// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using NuStreamDocs.MarkdownExtensions.AttrList;
using NuStreamDocs.MarkdownExtensions.AttrList.Bytes;
using NuStreamDocs.Mermaid;
using NuStreamDocs.Plugins;
using NuStreamDocs.Privacy;
using NuStreamDocs.Privacy.Bytes;

namespace NuStreamDocs.Benchmarks;

/// <summary>Throughput + allocation benchmarks for the HTML post-render plugins (attr-list, mermaid, privacy scanner) and the individual byte-level scanners they rely on.</summary>
[ShortRunJob]
[MemoryDiagnoser]
public class PostRenderBenchmarks
{
    /// <summary>Number of repeated blocks per fixture.</summary>
    private const int Repetitions = 100;

    /// <summary>Headroom factor for buffers that may grow under entity-style replacements (~10% expansion typical; 2× is generous).</summary>
    private const int OutputExpansionFactor = 2;

    /// <summary>Length of the literal <c>{:</c> attr-list opening marker.</summary>
    private const int OpenMarkerLength = 2;

    /// <summary>Pre-built attr-list fixture.</summary>
    private byte[] _attrListHtml = [];

    /// <summary>Pre-built mermaid fixture.</summary>
    private byte[] _mermaidHtml = [];

    /// <summary>Pre-built privacy fixture (mix of img/link/script/inline-style).</summary>
    private byte[] _privacyHtml = [];

    /// <summary>Mixed-content-only fixture for the byte scanner.</summary>
    private byte[] _mixedContentHtml = [];

    /// <summary>External-anchor-only fixture for the byte scanner.</summary>
    private byte[] _externalAnchorHtml = [];

    /// <summary>Asset-attribute-only fixture for the byte scanner.</summary>
    private byte[] _assetAttrHtml = [];

    /// <summary>Srcset-only fixture for the byte scanner.</summary>
    private byte[] _srcsetHtml = [];

    /// <summary>Inline-style-only fixture for the byte scanner.</summary>
    private byte[] _inlineStyleHtml = [];

    /// <summary>Synthetic source for the EmitMerged direct micro-benchmark — existing attrs followed by a <c>{:</c>...<c>}</c> body.</summary>
    private byte[] _emitMergedSource = [];

    /// <summary>Inclusive start of the existing-attrs window inside <see cref="_emitMergedSource"/>.</summary>
    private int _emitMergedExistingStart;

    /// <summary>Exclusive end of the existing-attrs window inside <see cref="_emitMergedSource"/>.</summary>
    private int _emitMergedExistingEnd;

    /// <summary>Inclusive start of the attr-list body inside <see cref="_emitMergedSource"/> (just past <c>{:</c>).</summary>
    private int _emitMergedAttrListStart;

    /// <summary>Exclusive end of the attr-list body inside <see cref="_emitMergedSource"/> (at the closing <c>}</c>).</summary>
    private int _emitMergedAttrListEnd;

    /// <summary>Shared registry used by URL-rewrite benchmarks.</summary>
    private ExternalAssetRegistry _registry = new("local"u8.ToArray());

    /// <summary>Shared host filter — accepts every host so every URL exercises the rewrite path.</summary>
    private HostFilter _filter = new(hostsToSkip: null, hostsAllowed: null);

    /// <summary>Generates the per-plugin input fixtures.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _attrListHtml = Repeat("<h1>Heading {: #intro .lead }</h1><p class=\"existing\">Body {: .extra .more }</p>");
        _mermaidHtml = Repeat("<pre><code class=\"language-mermaid\">graph TD; A--&gt;B; B--&gt;C</code></pre>");
        _privacyHtml = Repeat(
            "<img src=\"https://example.com/x.png\">"
            + "<link rel=\"stylesheet\" href=\"https://cdn.example/x.css\">"
            + "<style>body{background:url(https://cdn.example/bg.png)}</style>"
            + "<a href=\"https://docs.example/page\">link</a>");
        _mixedContentHtml = Repeat("<a href=\"http://example.com/page\">x</a><img src=\"http://cdn.example/a.png\">");
        _externalAnchorHtml = Repeat("<a href=\"https://example.com/page\">x</a><a href=\"https://docs.example\" rel=\"author\">y</a>");
        _assetAttrHtml = Repeat("<img src=\"https://cdn.example/a.png\"><link rel=\"stylesheet\" href=\"https://cdn.example/x.css\">");
        _srcsetHtml = Repeat("<img srcset=\"https://cdn.example/a.png 1x, https://cdn.example/b.png 2x, https://cdn.example/c.png 3x\">");
        _inlineStyleHtml = Repeat("<style>.x { background: url(https://cdn.example/a.png); border-image: url(\"https://cdn.example/b.png\"); }</style>");
        _registry = new("local"u8.ToArray());
        _filter = new(hostsToSkip: null, hostsAllowed: null);

        const string ExistingAttrs = " class=\"existing\" data-x=\"1\"";
        const string AttrListBody = " #intro .lead .extra target=\"_blank\" ";
        _emitMergedSource = Encoding.UTF8.GetBytes(ExistingAttrs + "{:" + AttrListBody + "}");
        _emitMergedExistingStart = 0;
        _emitMergedExistingEnd = ExistingAttrs.Length;
        _emitMergedAttrListStart = _emitMergedExistingEnd + OpenMarkerLength;
        _emitMergedAttrListEnd = _emitMergedSource.Length - 1;
    }

    /// <summary>Benchmark for <c>AttrListPlugin</c>'s post-render rewrite.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int AttrList() => RunPostRender(new AttrListPlugin(), _attrListHtml);

    /// <summary>Direct benchmark for <c>AttrListRewriter.RewriteInto</c> (skips the plugin context wrapping).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int AttrListDirect()
    {
        var sink = new ArrayBufferWriter<byte>(_attrListHtml.Length * 2);
        AttrListRewriter.RewriteInto(_attrListHtml, sink);
        return sink.WrittenCount;
    }

    /// <summary>Direct benchmark for <c>AttrListMarker.EmitMerged</c> over a single synthetic opening-tag fixture; isolates the merge hot path from the surrounding stage rotation.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int AttrListMarkerEmitMerged()
    {
        var sink = new ArrayBufferWriter<byte>(_emitMergedSource.Length * OutputExpansionFactor);
        for (var i = 0; i < Repetitions; i++)
        {
            AttrListMarker.EmitMerged(
                _emitMergedSource,
                _emitMergedExistingStart,
                _emitMergedExistingEnd,
                _emitMergedAttrListStart,
                _emitMergedAttrListEnd,
                sink);
        }

        return sink.WrittenCount;
    }

    /// <summary>Benchmark for <c>MermaidPlugin</c>'s post-render retag.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Mermaid() => RunPostRender(new MermaidPlugin(), _mermaidHtml);

    /// <summary>Benchmark for <c>PrivacyPlugin</c>'s page scan (full pipeline through PrivacyRewriter).</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int Privacy() => RunPostRender(new PrivacyPlugin(), _privacyHtml);

    /// <summary>Direct benchmark for <c>MixedContentBytes.RewriteInto</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int MixedContentBytesDirect()
    {
        var sink = new ArrayBufferWriter<byte>(_mixedContentHtml.Length * 2);
        MixedContentBytes.RewriteInto(_mixedContentHtml, sink);
        return sink.WrittenCount;
    }

    /// <summary>Direct benchmark for <c>AnchorBytes.RewriteInto</c> with rel + target hardening on.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int AnchorBytesDirect()
    {
        var sink = new ArrayBufferWriter<byte>(_externalAnchorHtml.Length * 2);
        AnchorBytes.RewriteInto(_externalAnchorHtml, addRelNoOpener: true, addTargetBlank: true, sink);
        return sink.WrittenCount;
    }

    /// <summary>Direct benchmark for <c>AssetAttributeBytes.RewriteInto</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int AssetAttributeBytesDirect()
    {
        var sink = new ArrayBufferWriter<byte>(_assetAttrHtml.Length * 2);
        var ctx = new UrlRewriteContext(_filter, _registry);
        AssetAttributeBytes.RewriteInto(_assetAttrHtml, ctx, sink);
        return sink.WrittenCount;
    }

    /// <summary>Direct benchmark for <c>SrcsetBytes.RewriteInto</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int SrcsetBytesDirect()
    {
        var sink = new ArrayBufferWriter<byte>(_srcsetHtml.Length * 2);
        var ctx = new UrlRewriteContext(_filter, _registry);
        SrcsetBytes.RewriteInto(_srcsetHtml, ctx, sink);
        return sink.WrittenCount;
    }

    /// <summary>Direct benchmark for <c>InlineStyleBlockBytes.RewriteInto</c>.</summary>
    /// <returns>Bytes written.</returns>
    [Benchmark]
    public int InlineStyleBlockBytesDirect()
    {
        var sink = new ArrayBufferWriter<byte>(_inlineStyleHtml.Length * 2);
        var ctx = new UrlRewriteContext(_filter, _registry);
        InlineStyleBlockBytes.RewriteInto(_inlineStyleHtml, ctx, sink);
        return sink.WrittenCount;
    }

    /// <summary>Drives <paramref name="plugin"/>.PostRender against <paramref name="html"/>.</summary>
    /// <param name="plugin">Post-render plugin under test.</param>
    /// <param name="html">Pre-rendered HTML bytes.</param>
    /// <returns>Bytes written by the plugin.</returns>
    private static int RunPostRender(IPagePostRenderPlugin plugin, byte[] html)
    {
        var sink = new ArrayBufferWriter<byte>(html.Length * 2);
        var context = new PagePostRenderContext("page.md", default, html, sink);
        plugin.PostRender(in context);
        return sink.WrittenCount;
    }

    /// <summary>Stamps <paramref name="block"/> <c>Repetitions</c> times.</summary>
    /// <param name="block">Source fragment.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] Repeat(string block)
    {
        var sb = new StringBuilder(block.Length * Repetitions);
        for (var i = 0; i < Repetitions; i++)
        {
            sb.Append(block);
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
