// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Plugins;
using NuStreamDocs.Theme.Material;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <c>MaterialThemePlugin.PostRender</c> with a pre-built body.</summary>
/// <remarks>
/// The plugin's hot path used to allocate one fresh <c>byte[]</c> per
/// page for the body copy plus several <c>byte[]</c>s for option-string
/// re-encodes. After the pooling refactor (task #55) those should be
/// gone — this benchmark surfaces any regression and gives the
/// allocation profiler something focused to chew on.
/// </remarks>
[ShortRunJob]
[MemoryDiagnoser]
public class ThemeWrapBenchmarks
{
    /// <summary>Body byte count used to size the synthetic page.</summary>
    private const int BodyBytes = 8 * 1024;

    /// <summary>Buffer-growth factor used to size the html writer to comfortably hold the wrapped page.</summary>
    private const int WrappedPageGrowthFactor = 4;

    /// <summary>Pre-configured plugin instance reused across iterations.</summary>
    private MaterialThemePlugin _plugin = null!;

    /// <summary>Pre-built UTF-8 body bytes copied into <c>html</c> at the start of every iteration.</summary>
    private byte[] _bodyTemplate = [];

    /// <summary>The <c>ArrayBufferWriter{T}</c> the plugin writes the wrapped page into.</summary>
    private ArrayBufferWriter<byte> _html = null!;

    /// <summary>Configures the plugin and generates the body fixture once.</summary>
    /// <returns>A task representing the asynchronous setup.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _plugin = new();
        BuildConfigureContext configureContext = new("/in", "/out", [_plugin], new());
        await _plugin.ConfigureAsync(configureContext, CancellationToken.None);

        StringBuilder sb = new(BodyBytes);
        while (sb.Length < BodyBytes)
        {
            sb.Append("<p>This is a paragraph that fills the synthetic page body. <a href=\"x\">link</a> &amp; more.</p>\n");
        }

        _bodyTemplate = Encoding.UTF8.GetBytes(sb.ToString());
        _html = new(BodyBytes * WrappedPageGrowthFactor);
    }

    /// <summary>Resets the output writer before each iteration so the plugin sees a fresh sink.</summary>
    [IterationSetup]
    public void IterationSetup() => _html.ResetWrittenCount();

    /// <summary>One <c>MaterialThemePlugin.PostRender</c> invocation.</summary>
    /// <returns>Wrapped page byte count.</returns>
    [Benchmark]
    public int Wrap()
    {
        PagePostRenderContext renderContext = new("guide/page.md", default, _bodyTemplate, _html);
        _plugin.PostRender(in renderContext);
        return _html.WrittenCount;
    }
}
