// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
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
/// rendered HTML once, walk it forward looking for <c>&lt;code class="language-…"&gt;</c>,
/// emit prefix bytes verbatim, swap the body for highlighted bytes, repeat.
/// One copy per page; per-block work is the lexer pass plus an HTML escape.
/// </remarks>
public sealed class HighlightPlugin : IDocPlugin
{
    /// <summary>The opening tag prefix scanned for during the replace pass.</summary>
    private static readonly byte[] CodeOpenPrefix = [.. "<code class=\"language-"u8];

    /// <summary>The closing tag pattern that terminates a highlighted body.</summary>
    private static readonly byte[] CodeClose = [.. "</code>"u8];

    /// <summary>The lexer registry built once at configure time.</summary>
    private readonly LexerRegistry _registry;

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
        if (html.WrittenSpan.IndexOf("<code class=\"language-"u8) < 0)
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

    /// <summary>Reverses the small escape set <see cref="NuStreamDocs.Html.HtmlEscape.EscapeText"/> produces.</summary>
    /// <param name="body">UTF-8 escaped body.</param>
    /// <returns>Decoded source string.</returns>
    private static string HtmlUnescape(ReadOnlySpan<byte> body)
    {
        // Bodies without an ampersand take the no-decode fast path: one
        // GetString call. Bodies with entities decode byte-to-byte through
        // the shared HtmlEntityDecoder (rented buffer, no intermediate
        // string allocations) and then convert once at the end. Avoids
        // the chained string.Replace tower the previous implementation
        // walked through.
        if (body.IndexOf((byte)'&') < 0)
        {
            return Encoding.UTF8.GetString(body);
        }

        var sink = new ArrayBufferWriter<byte>(body.Length);
        HtmlEntityDecoder.DecodeInto(sink, body);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
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

    /// <summary>Walks <paramref name="source"/>, copying through verbatim and substituting highlighted bodies for every recognised <c>&lt;code class="language-…"&gt;</c> block.</summary>
    /// <param name="source">Snapshot of the rendered HTML.</param>
    /// <param name="writer">UTF-8 sink (the original page buffer).</param>
    private void Highlight(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var cursor = 0;
        while (cursor < source.Length)
        {
            var rest = source[cursor..];
            var openIdx = rest.IndexOf(CodeOpenPrefix);
            if (openIdx < 0)
            {
                Write(writer, rest);
                return;
            }

            var openAbs = cursor + openIdx;
            Write(writer, source[cursor..openAbs]);

            var langStart = openAbs + CodeOpenPrefix.Length;
            var langEnd = source[langStart..].IndexOf((byte)'"');
            if (langEnd <= 0)
            {
                Write(writer, source[openAbs..]);
                return;
            }

            var language = Encoding.UTF8.GetString(source.Slice(langStart, langEnd));
            var bodyStart = langStart + langEnd + 2; // skip the "> after the language attr
            if (bodyStart > source.Length)
            {
                Write(writer, source[openAbs..]);
                return;
            }

            var bodyEndRel = source[bodyStart..].IndexOf(CodeClose);
            if (bodyEndRel < 0)
            {
                Write(writer, source[openAbs..]);
                return;
            }

            var bodyEnd = bodyStart + bodyEndRel;
            Write(writer, source[openAbs..bodyStart]);

            var body = source.Slice(bodyStart, bodyEnd - bodyStart);
            EmitBody(writer, language, body);

            Write(writer, CodeClose);
            cursor = bodyEnd + CodeClose.Length;
        }
    }

    /// <summary>Highlights <paramref name="body"/> when a lexer is registered for <paramref name="language"/>; otherwise emits it verbatim.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="language">Language name from the class attribute.</param>
    /// <param name="body">Body bytes between <c>&gt;</c> and <c>&lt;/code&gt;</c>.</param>
    private void EmitBody(IBufferWriter<byte> writer, string language, ReadOnlySpan<byte> body)
    {
        if (!_registry.TryGet(language, out var lexer))
        {
            Write(writer, body);
            return;
        }

        // The body has already been HTML-escaped by the markdown emitter.
        // Decode it back to a plain string for the lexer (regex APIs work
        // on strings), then re-escape during the highlighted emit.
        var unescaped = HtmlUnescape(body);
        HighlightEmitter.Emit(lexer, unescaped, writer);
    }
}
