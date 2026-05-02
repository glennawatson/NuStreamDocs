// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Plugin that finds <c>&lt;pre&gt;&lt;code class="language-X"&gt;…&lt;/code&gt;&lt;/pre&gt;</c>
/// blocks emitted by the renderer and replaces the body with classed token spans
/// produced by a <see cref="Lexer"/>.
/// </summary>
/// <remarks>
/// Match the marker-replace pattern used by the Nav plugin: snapshot the
/// rendered HTML once, walk it forward looking for <c>&lt;pre&gt;&lt;code class="language-…"&gt;</c>,
/// emit prefix bytes verbatim, swap the body for highlighted bytes, repeat.
/// One copy per page; per-block work is the lexer pass plus an HTML escape.
/// <para>
/// The whole pipeline stays UTF-8: the language alias and the unescaped
/// body are passed to the lexer / emitter as <see cref="ReadOnlyMemory{Byte}"/>
/// without any UTF-16 round-trip.
/// </para>
/// </remarks>
public sealed class HighlightPlugin : IDocPlugin
{
    /// <summary>The opening tag the rewriter searches for.</summary>
    private static readonly byte[] PreOpen = [.. "<pre><code class=\"language-"u8];

    /// <summary>The closing tag pattern that terminates a highlighted body.</summary>
    private static readonly byte[] CodeClose = [.. "</code>"u8];

    /// <summary>Closing of the surrounding <c>&lt;/pre&gt;</c>.</summary>
    private static readonly byte[] PreClose = [.. "</pre>"u8];

    /// <summary>The lexer registry built once at configure time.</summary>
    private readonly LexerRegistry _registry;

    /// <summary>Configured options.</summary>
    private readonly HighlightOptions _options;

    /// <summary>Initializes a new instance of the <see cref="HighlightPlugin"/> class with default options.</summary>
    public HighlightPlugin()
        : this(HighlightOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="HighlightPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public HighlightPlugin(HighlightOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _registry = LexerRegistry.Build(options.ExtraLexers);
        _options = options;
    }

    /// <inheritdoc/>
    public string Name => "highlight";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var html = context.Html;
        if (html.WrittenSpan.IndexOf("<pre><code class=\"language-"u8) < 0)
        {
            return ValueTask.CompletedTask;
        }

        HtmlSnapshotRewriter.Rewrite(html, this, static (snapshot, writer, self) => self.Highlight(snapshot, writer));
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinaliseAsync(PluginFinaliseContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <summary>Decodes UTF-8 HTML-escaped bytes into a fresh byte array (no UTF-16 round-trip). When the input contains no entities the source bytes are copied verbatim.</summary>
    /// <param name="bytes">Source bytes.</param>
    /// <returns>Decoded bytes.</returns>
    private static byte[] HtmlUnescapeBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IndexOf((byte)'&') < 0)
        {
            return bytes.ToArray();
        }

        var sink = new ArrayBufferWriter<byte>(bytes.Length);
        HtmlEntityDecoder.DecodeInto(sink, bytes);
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Slices the <c>data-info</c> attribute value out of the <c>&lt;code …&gt;</c> opening, or returns empty when absent.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="afterLang">Offset just past the closing quote of <c>language-X</c>.</param>
    /// <param name="openTagEnd">Offset of the closing <c>&gt;</c> of the open tag.</param>
    /// <returns>Decoded fence-info bytes (HTML entities unescaped).</returns>
    private static byte[] ExtractDataInfo(ReadOnlySpan<byte> source, int afterLang, int openTagEnd)
    {
        var attrs = source[afterLang..openTagEnd];
        var marker = " data-info=\""u8;
        var idx = attrs.IndexOf(marker);
        if (idx < 0)
        {
            return [];
        }

        var valStart = idx + marker.Length;
        var endRel = attrs[valStart..].IndexOf((byte)'"');
        if (endRel < 0)
        {
            return [];
        }

        return HtmlUnescapeBytes(attrs.Slice(valStart, endRel));
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Walks <paramref name="source"/>, copying through verbatim and substituting highlighted bodies for every recognised <c>&lt;pre&gt;&lt;code class="language-…"&gt;</c> block.</summary>
    /// <param name="source">Snapshot of the rendered HTML.</param>
    /// <param name="writer">UTF-8 sink (the original page buffer).</param>
    private void Highlight(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var cursor = 0;
        while (cursor < source.Length)
        {
            var rest = source[cursor..];
            var openIdx = rest.IndexOf(PreOpen);
            if (openIdx < 0)
            {
                Write(writer, rest);
                return;
            }

            var preStart = cursor + openIdx;
            Write(writer, source[cursor..preStart]);

            cursor = ProcessBlock(source, preStart, writer);
        }
    }

    /// <summary>Processes one matched <c>&lt;pre&gt;&lt;code class="language-…"&gt;</c> block; emits the rewritten form and returns the offset to resume scanning at.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="preStart">Offset of <c>&lt;pre&gt;</c>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Offset just past the closing <c>&lt;/pre&gt;</c> when matched, otherwise the offset where the failed match began (so the scanner advances).</returns>
    private int ProcessBlock(ReadOnlySpan<byte> source, int preStart, IBufferWriter<byte> writer)
    {
        var langStart = preStart + PreOpen.Length;
        var langEndRel = source[langStart..].IndexOf((byte)'"');
        if (langEndRel <= 0)
        {
            Write(writer, source[preStart..]);
            return source.Length;
        }

        var languageBytes = source.Slice(langStart, langEndRel);
        var afterLang = langStart + langEndRel + 1;
        var openTagEndRel = source[afterLang..].IndexOf((byte)'>');
        if (openTagEndRel < 0)
        {
            Write(writer, source[preStart..]);
            return source.Length;
        }

        var openTagEnd = afterLang + openTagEndRel;
        var dataInfo = ExtractDataInfo(source, afterLang, openTagEnd);
        var bodyStart = openTagEnd + 1;
        var bodyEndRel = source[bodyStart..].IndexOf(CodeClose);
        if (bodyEndRel < 0)
        {
            Write(writer, source[preStart..]);
            return source.Length;
        }

        var bodyEnd = bodyStart + bodyEndRel;
        var afterCodeClose = bodyEnd + CodeClose.Length;
        var preCloseRel = source[afterCodeClose..].IndexOf(PreClose);
        if (preCloseRel < 0)
        {
            Write(writer, source[preStart..]);
            return source.Length;
        }

        EmitWrappedBlock(source, preStart, bodyStart, bodyEnd, dataInfo, languageBytes, writer);
        return afterCodeClose + preCloseRel + PreClose.Length;
    }

    /// <summary>Emits the rewritten block (wrapper div + optional title + optional copy button + the highlighted body).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="preStart">Offset of <c>&lt;pre&gt;</c>.</param>
    /// <param name="bodyStart">Offset of the first body byte.</param>
    /// <param name="bodyEnd">Offset of <c>&lt;/code&gt;</c>.</param>
    /// <param name="dataInfo">Decoded fence-info bytes (may be empty).</param>
    /// <param name="languageBytes">Language alias bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitWrappedBlock(ReadOnlySpan<byte> source, int preStart, int bodyStart, int bodyEnd, ReadOnlySpan<byte> dataInfo, ReadOnlySpan<byte> languageBytes, IBufferWriter<byte> writer)
    {
        EmitOpeningWrapper(dataInfo, writer);

        // The original <pre><code class="language-X"…> opening — we rewrite the body, the rest passes through.
        Write(writer, source[preStart..bodyStart]);
        var body = source[bodyStart..bodyEnd];
        EmitBody(writer, languageBytes, body);
        Write(writer, CodeClose);
        Write(writer, PreClose);

        EmitClosingWrapper(writer);
    }

    /// <summary>Emits the opening wrapper div + title + copy button when <see cref="HighlightOptions.WrapInHighlightDiv"/> is on.</summary>
    /// <param name="dataInfo">Fence-info bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitOpeningWrapper(ReadOnlySpan<byte> dataInfo, IBufferWriter<byte> writer)
    {
        if (!_options.WrapInHighlightDiv)
        {
            return;
        }

        Write(writer, "<div class=\"highlight\">"u8);
        EmitTitleBar(dataInfo, writer);
        EmitCopyButton(writer);
    }

    /// <summary>Emits the closing wrapper <c>&lt;/div&gt;</c> when <see cref="HighlightOptions.WrapInHighlightDiv"/> is on.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitClosingWrapper(IBufferWriter<byte> writer)
    {
        if (!_options.WrapInHighlightDiv)
        {
            return;
        }

        Write(writer, "</div>"u8);
    }

    /// <summary>Emits <c>&lt;span class="filename"&gt;{title}&lt;/span&gt;</c> when <see cref="HighlightOptions.EmitTitleBar"/> is on and a title attribute is present.</summary>
    /// <param name="dataInfo">Fence-info bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitTitleBar(ReadOnlySpan<byte> dataInfo, IBufferWriter<byte> writer)
    {
        if (!_options.EmitTitleBar || dataInfo.IsEmpty)
        {
            return;
        }

        if (!FenceAttrParser.TryGetTitle(dataInfo, out var title) || title.IsEmpty)
        {
            return;
        }

        Write(writer, "<span class=\"filename\">"u8);
        Html.HtmlEscape.EscapeText(title, writer);
        Write(writer, "</span>"u8);
    }

    /// <summary>Emits a copy button (theme JS handles the click) when <see cref="HighlightOptions.CopyButton"/> is on.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    private void EmitCopyButton(IBufferWriter<byte> writer)
    {
        if (!_options.CopyButton)
        {
            return;
        }

        Write(writer, "<button class=\"md-clipboard md-icon\" type=\"button\" aria-label=\"Copy to clipboard\"></button>"u8);
    }

    /// <summary>Highlights <paramref name="body"/> when a lexer is registered for <paramref name="languageBytes"/>; otherwise emits it verbatim.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="languageBytes">Language alias bytes from the class attribute.</param>
    /// <param name="body">Body bytes between <c>&gt;</c> and <c>&lt;/code&gt;</c>.</param>
    private void EmitBody(IBufferWriter<byte> writer, ReadOnlySpan<byte> languageBytes, ReadOnlySpan<byte> body)
    {
        if (!_registry.TryGet(languageBytes, out var lexer) || lexer is null)
        {
            Write(writer, body);
            return;
        }

        var unescaped = HtmlUnescapeBytes(body);
        HighlightEmitter.Emit(lexer, unescaped, writer);
    }
}
