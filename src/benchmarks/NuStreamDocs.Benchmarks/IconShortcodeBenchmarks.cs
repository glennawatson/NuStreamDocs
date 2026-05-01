// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Icons.MaterialDesign;
using NuStreamDocs.Theme.Material.IconShortcode;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the icon-shortcode rewriter and the MDI inline-SVG resolver.</summary>
/// <remarks>
/// Splits the cost into two layers: <see cref="MdiResolverDirect"/> measures
/// the raw catalogue lookup (the inner loop the rewriter calls) and
/// <see cref="IconShortcodesWithMdi"/> measures the rewriter end-to-end with
/// the resolver wired in.
/// </remarks>
[MemoryDiagnoser]
public class IconShortcodeBenchmarks
{
    /// <summary>Number of <c>:material-foo:</c> shortcodes stamped into each fixture.</summary>
    private const int Repetitions = 100;

    /// <summary>Pre-built fixture mixing MDI hits + ligature fallbacks + Font Awesome shortcodes.</summary>
    private byte[] _mixedSource = [];

    /// <summary>Configured plugin with the MDI resolver wired in.</summary>
    private IconShortcodePlugin _withMdi = null!;

    /// <summary>Configured plugin without an inline-SVG resolver — emits font-ligature spans.</summary>
    private IconShortcodePlugin _ligatureOnly = null!;

    /// <summary>Pre-built MDI resolver — exercised directly by <see cref="MdiResolverDirect"/>.</summary>
    private MdiIconResolver _mdiResolver = null!;

    /// <summary>Byte fixture for the resolver-only benchmark — known MDI hit.</summary>
    private byte[] _resolverHit = [];

    /// <summary>Byte fixture for the resolver-only benchmark — guaranteed miss.</summary>
    private byte[] _resolverMiss = [];

    /// <summary>Allocates fixtures + plugins.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _mixedSource = BuildRepeated(
            ":material-rocket-launch: ship :material-source-branch: branch :material-not-an-icon: miss :fontawesome-brands-github: site\n");
        _resolverHit = Encoding.UTF8.GetBytes("rocket-launch");
        _resolverMiss = Encoding.UTF8.GetBytes("definitely-not-a-real-icon-name");

        _mdiResolver = new MdiIconResolver();
        _withMdi = new IconShortcodePlugin(_mdiResolver);
        _ligatureOnly = new IconShortcodePlugin();
    }

    /// <summary>MDI resolver hot-path — direct <c>TryResolve</c> for a known hit.</summary>
    /// <returns>SVG length on hit; 0 on miss.</returns>
    [Benchmark]
    public int MdiResolverDirect() =>
        _mdiResolver.TryResolve(_resolverHit, out var svg) ? svg.Length : 0;

    /// <summary>MDI resolver miss-path — <c>TryResolve</c> with a name that walks the full per-length bucket and falls through.</summary>
    /// <returns>0 (always misses).</returns>
    [Benchmark]
    public int MdiResolverMiss() =>
        _mdiResolver.TryResolve(_resolverMiss, out _) ? 1 : 0;

    /// <summary>Icon shortcode rewriter end-to-end with the MDI resolver — inlines SVGs for known names, falls back for the rest.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int IconShortcodesWithMdi()
    {
        var sink = new ArrayBufferWriter<byte>(_mixedSource.Length * 4);
        _withMdi.Preprocess(_mixedSource, sink);
        return sink.WrittenCount;
    }

    /// <summary>Icon shortcode rewriter without a resolver — every Material shortcode emits a font-ligature span (the legacy path).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int IconShortcodesLigatureOnly()
    {
        var sink = new ArrayBufferWriter<byte>(_mixedSource.Length * 2);
        _ligatureOnly.Preprocess(_mixedSource, sink);
        return sink.WrittenCount;
    }

    /// <summary>Stamps <paramref name="block"/> <see cref="Repetitions"/> times into a UTF-8 buffer.</summary>
    /// <param name="block">Source fragment.</param>
    /// <returns>Pre-built fixture bytes.</returns>
    private static byte[] BuildRepeated(string block)
    {
        var blockBytes = Encoding.UTF8.GetBytes(block);
        var output = new byte[blockBytes.Length * Repetitions];
        for (var i = 0; i < Repetitions; i++)
        {
            blockBytes.AsSpan().CopyTo(output.AsSpan(i * blockBytes.Length));
        }

        return output;
    }
}
