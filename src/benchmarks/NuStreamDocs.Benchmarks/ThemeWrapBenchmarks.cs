// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Plugins;
using NuStreamDocs.Theme.Material;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for <c>MaterialThemePlugin.OnRenderPageAsync</c> with a pre-built body.</summary>
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

    /// <summary>The <c>ArrayBufferWriter{T}</c> the plugin reads + writes.</summary>
    private ArrayBufferWriter<byte> _html = null!;

    /// <summary>Configures the plugin and generates the body fixture once.</summary>
    /// <returns>A task representing the asynchronous setup.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _plugin = new();
        var configureContext = new PluginConfigureContext(default, "/in", "/out", []);
        await _plugin.OnConfigureAsync(configureContext, CancellationToken.None);

        var sb = new StringBuilder(BodyBytes);
        while (sb.Length < BodyBytes)
        {
            sb.Append("<p>This is a paragraph that fills the synthetic page body. <a href=\"x\">link</a> &amp; more.</p>\n");
        }

        _bodyTemplate = Encoding.UTF8.GetBytes(sb.ToString());
        _html = new(BodyBytes * WrappedPageGrowthFactor);
    }

    /// <summary>Restores the body bytes into <c>html</c> before each iteration so the plugin sees a fresh page.</summary>
    [IterationSetup]
    public void IterationSetup()
    {
        _html.ResetWrittenCount();
        _bodyTemplate.CopyTo(_html.GetSpan(_bodyTemplate.Length));
        _html.Advance(_bodyTemplate.Length);
    }

    /// <summary>One <c>MaterialThemePlugin.OnRenderPageAsync</c> invocation.</summary>
    /// <returns>Wrapped page byte count.</returns>
    [Benchmark]
    public async Task<int> Wrap()
    {
        var renderContext = new PluginRenderContext("guide/page.md", _bodyTemplate, _html);
        await _plugin.OnRenderPageAsync(renderContext, CancellationToken.None);
        return _html.WrittenCount;
    }
}
