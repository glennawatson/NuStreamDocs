// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.Common;
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
[ShortRunJob]
[MemoryDiagnoser]
public class IconShortcodeBenchmarks
{
    /// <summary>Number of <c>:material-foo:</c> shortcodes stamped into each fixture.</summary>
    private const int Repetitions = 100;

    /// <summary>Capacity for the resolver-direct sink — sized to fit one wrapped MDI SVG (wrapper ~80 B + path ~250 B).</summary>
    private const int ResolverSinkCapacity = 512;

    /// <summary>Headroom factor for the MDI rewrite output writer (each <c>:material-…:</c> shortcode expands to ~300-byte inline SVG; 4× covers it with margin).</summary>
    private const int MdiOutputExpansionFactor = 4;

    /// <summary>Headroom factor for the ligature-only output writer (each shortcode expands to a small <c>&lt;span&gt;</c> wrapper; 2× is generous).</summary>
    private const int LigatureOutputExpansionFactor = 2;

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

    /// <summary>Pre-allocated sink used by the resolver-direct benchmarks; reset between iterations.</summary>
    private ArrayBufferWriter<byte> _resolverSink = null!;

    /// <summary>MDI rewrite output sink size hint — sized once in <c>Setup</c>.</summary>
    private int _mdiHint;

    /// <summary>Ligature rewrite output sink size hint — sized once in <c>Setup</c>.</summary>
    private int _ligatureHint;

    /// <summary>Allocates fixtures + plugins.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _mixedSource = BuildRepeated(
            ":material-rocket-launch: ship :material-source-branch: branch :material-not-an-icon: miss :fontawesome-brands-github: site\n");
        _resolverHit = "rocket-launch"u8.ToArray();
        _resolverMiss = "definitely-not-a-real-icon-name"u8.ToArray();

        _mdiResolver = new();
        _withMdi = new(_mdiResolver);
        _ligatureOnly = new();
        _resolverSink = new(ResolverSinkCapacity);
        _mdiHint = _mixedSource.Length * MdiOutputExpansionFactor;
        _ligatureHint = _mixedSource.Length * LigatureOutputExpansionFactor;
    }

    /// <summary>MDI resolver hot-path — direct <c>TryResolve</c> for a known hit, writes wrapped SVG to a pre-sized sink.</summary>
    /// <returns>Bytes written on hit; 0 on miss.</returns>
    [Benchmark]
    public int MdiResolverDirect()
    {
        _resolverSink.ResetWrittenCount();
        return _mdiResolver.TryResolve(_resolverHit, _resolverSink) ? _resolverSink.WrittenCount : 0;
    }

    /// <summary>MDI resolver miss-path — <c>TryResolve</c> with a name that walks the full per-length bucket and falls through.</summary>
    /// <returns>0 (always misses).</returns>
    [Benchmark]
    public int MdiResolverMiss()
    {
        _resolverSink.ResetWrittenCount();
        return _mdiResolver.TryResolve(_resolverMiss, _resolverSink) ? 1 : 0;
    }

    /// <summary>Icon shortcode rewriter end-to-end with the MDI resolver, renting from <see cref="PageBuilderPool"/> to mirror production.</summary>
    /// <remarks>Inlines SVGs for known names; falls back to the font-ligature span for the rest.</remarks>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int IconShortcodesWithMdi()
    {
        using var rental = PageBuilderPool.Rent(_mdiHint);
        _withMdi.Preprocess(_mixedSource, rental.Writer);
        return rental.Writer.WrittenCount;
    }

    /// <summary>Icon shortcode rewriter without a resolver, renting from <see cref="PageBuilderPool"/> to mirror production.</summary>
    /// <remarks>Every Material shortcode emits a font-ligature span (the legacy path).</remarks>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int IconShortcodesLigatureOnly()
    {
        using var rental = PageBuilderPool.Rent(_ligatureHint);
        _ligatureOnly.Preprocess(_mixedSource, rental.Writer);
        return rental.Writer.WrittenCount;
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
