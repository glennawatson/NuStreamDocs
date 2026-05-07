// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.Footnotes;

/// <summary>
/// Stateless UTF-8 footnotes rewriter. Two passes over the source:
/// the first collects every <c>[^id]: definition</c> block (and
/// strips it from the output), the second replaces each <c>[^id]</c>
/// reference with a linked superscript. A trailing
/// <c>&lt;section class="footnotes"&gt;</c> with the collected
/// definitions is appended.
/// </summary>
internal static class FootnotesRewriter
{
    /// <summary>Length of the <c>"[^"</c> reference prefix.</summary>
    private const int ReferencePrefixLength = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        List<Definition> defs = [];
        CollectAndStripDefs(source, defs, writer);
        if (defs.Count is 0)
        {
            return;
        }

        EmitSection(defs, writer);
    }

    /// <summary>Walks <paramref name="source"/> once, collecting definitions and emitting non-definition bytes (with <c>[^id]</c> references rewritten) to <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="defs">Definition list populated in encounter order.</param>
    /// <param name="writer">UTF-8 sink for the body output.</param>
    /// <remarks>
    /// Skips fenced code blocks and inline code spans verbatim so that bracket-heavy fragments inside code
    /// (e.g. the regex character class <c>[^@\s]</c>) don't accidentally register as footnote references.
    /// </remarks>
    private static void CollectAndStripDefs(ReadOnlySpan<byte> source, List<Definition> defs, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (TrySkipCodeSpan(source, i, writer, out var afterCode))
            {
                i = afterCode;
                continue;
            }

            if (TryConsumeDefinition(source, i, defs, out var afterDef))
            {
                i = afterDef;
                continue;
            }

            if (TryEmitInlineReference(source, i, defs, writer, out var afterRef))
            {
                i = afterRef;
                continue;
            }

            var dest = writer.GetSpan(1);
            dest[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }

    /// <summary>Skips a fenced or inline code span verbatim into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="afterCode">Cursor position past the consumed span on success.</param>
    /// <returns>True when a code span was consumed.</returns>
    private static bool TrySkipCodeSpan(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int afterCode)
    {
        if (MarkdownCodeScanner.AtLineStart(source, offset)
            && MarkdownCodeScanner.TryConsumeFence(source, offset, out var fenceEnd))
        {
            writer.Write(source[offset..fenceEnd]);
            afterCode = fenceEnd;
            return true;
        }

        if (source[offset] == (byte)'`')
        {
            var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, offset);
            writer.Write(source[offset..inlineEnd]);
            afterCode = inlineEnd;
            return true;
        }

        afterCode = offset;
        return false;
    }

    /// <summary>Tries to consume a <c>[^id]: definition</c> block at the line-start position <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="defs">Definition accumulator (mutated on success).</param>
    /// <param name="afterDef">Cursor position past the consumed block on success.</param>
    /// <returns>True when a definition was consumed.</returns>
    private static bool TryConsumeDefinition(ReadOnlySpan<byte> source, int offset, List<Definition> defs, out int afterDef)
    {
        afterDef = offset;
        if (!MarkdownCodeScanner.AtLineStart(source, offset)
            || !TryParseDefinition(source, offset, out var idStart, out var idLen, out var bodyStart, out var bodyEnd, out var blockEnd))
        {
            return false;
        }

        defs.Add(new(
            source.Slice(idStart, idLen).ToArray(),
            [.. source[bodyStart..bodyEnd]]));
        afterDef = blockEnd;
        return true;
    }

    /// <summary>Tries to emit an inline <c>[^id]</c> reference at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="defs">Known definitions / forward placeholders.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="afterRef">Cursor position past the consumed reference on success.</param>
    /// <returns>True when a reference was emitted.</returns>
    private static bool TryEmitInlineReference(ReadOnlySpan<byte> source, int offset, List<Definition> defs, IBufferWriter<byte> writer, out int afterRef)
    {
        afterRef = offset;
        if (source[offset] != (byte)'['
            || offset + 1 >= source.Length
            || source[offset + 1] != (byte)'^'
            || !TryParseInlineRef(source, offset, out var refIdStart, out var refIdLen, out var refEnd))
        {
            return false;
        }

        EmitReference(source.Slice(refIdStart, refIdLen), defs, writer);
        afterRef = refEnd;
        return true;
    }

    /// <summary>Tries to parse a <c>[^id]: definition</c> block at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Candidate line-start offset.</param>
    /// <param name="idStart">Offset of the id token on success.</param>
    /// <param name="idLen">Length of the id token on success.</param>
    /// <param name="bodyStart">Offset of the first definition byte on success.</param>
    /// <param name="bodyEnd">Exclusive end of the definition body on success.</param>
    /// <param name="blockEnd">Exclusive end of the entire block (consumes trailing newline).</param>
    /// <returns>True when a definition block was found.</returns>
    private static bool TryParseDefinition(
        ReadOnlySpan<byte> source,
        int offset,
        out int idStart,
        out int idLen,
        out int bodyStart,
        out int bodyEnd,
        out int blockEnd)
    {
        bodyStart = 0;
        bodyEnd = 0;
        blockEnd = 0;

        if (!TryParseId(source, offset, out idStart, out idLen, out var afterId))
        {
            return false;
        }

        if (afterId + 1 >= source.Length || source[afterId] != (byte)']' || source[afterId + 1] != (byte)':')
        {
            return false;
        }

        var p = afterId + ReferencePrefixLength;
        if (p < source.Length && source[p] == (byte)' ')
        {
            p++;
        }

        bodyStart = p;
        var lineEnd = MarkdownCodeScanner.LineEnd(source, p);
        bodyEnd = bodyStart + AsciiByteHelpers.TrimTrailingNewline(source[bodyStart..lineEnd]).Length;
        blockEnd = lineEnd;
        return true;
    }

    /// <summary>Tries to consume the <c>[^id</c> opener and yields the id slice.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Candidate offset of <c>[</c>.</param>
    /// <param name="idStart">Offset of the id token on success.</param>
    /// <param name="idLen">Length of the id token on success.</param>
    /// <param name="afterId">Offset of the byte just past the id (typically <c>]</c>).</param>
    /// <returns>True when an id was consumed.</returns>
    private static bool TryParseId(ReadOnlySpan<byte> source, int offset, out int idStart, out int idLen, out int afterId)
    {
        idStart = 0;
        idLen = 0;
        afterId = 0;
        if (offset + ReferencePrefixLength >= source.Length || source[offset] != (byte)'[' || source[offset + 1] != (byte)'^')
        {
            return false;
        }

        var p = offset + ReferencePrefixLength;
        idStart = p;
        while (p < source.Length && source[p] != (byte)']' && source[p] != (byte)'\n')
        {
            p++;
        }

        idLen = p - idStart;
        afterId = p;
        return idLen > 0;
    }

    /// <summary>Tries to parse an inline <c>[^id]</c> reference at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Offset of the leading <c>[</c>.</param>
    /// <param name="idStart">Offset of the id token on success.</param>
    /// <param name="idLen">Length of the id token on success.</param>
    /// <param name="refEnd">Exclusive end of the reference token on success.</param>
    /// <returns>True when an inline reference was found.</returns>
    private static bool TryParseInlineRef(ReadOnlySpan<byte> source, int offset, out int idStart, out int idLen, out int refEnd)
    {
        idLen = 0;
        refEnd = 0;
        var p = offset + ReferencePrefixLength;
        idStart = p;
        while (p < source.Length && source[p] != (byte)']' && source[p] != (byte)'\n')
        {
            p++;
        }

        if (p >= source.Length || source[p] != (byte)']')
        {
            return false;
        }

        idLen = p - idStart;
        if (idLen is 0)
        {
            return false;
        }

        refEnd = p + 1;
        return true;
    }

    /// <summary>Emits a superscript link to the matching definition (or a one-based ordinal when the id isn't yet known).</summary>
    /// <param name="id">UTF-8 id bytes.</param>
    /// <param name="defs">Definition list collected so far.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitReference(ReadOnlySpan<byte> id, List<Definition> defs, IBufferWriter<byte> writer)
    {
        var index = IndexOf(defs, id);
        if (index < 0)
        {
            // Forward reference: keep order by registering a placeholder.
            defs.Add(new(id.ToArray(), []));
            index = defs.Count - 1;
        }

        var ordinal = index + 1;
        writer.Write("<sup id=\"fnref-"u8);
        writer.Write(id);
        writer.Write("\"><a href=\"#fn-"u8);
        writer.Write(id);
        writer.Write("\">"u8);
        WriteInt(ordinal, writer);
        writer.Write("</a></sup>"u8);
    }

    /// <summary>Emits the trailing <c>&lt;section class="footnotes"&gt;</c>.</summary>
    /// <param name="defs">Collected definitions.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitSection(List<Definition> defs, IBufferWriter<byte> writer)
    {
        writer.Write("\n\n<section class=\"footnotes\">\n<ol>\n"u8);
        for (var i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            writer.Write("<li id=\"fn-"u8);
            writer.Write(def.Id);
            writer.Write("\">"u8);
            InlineRenderer.Render(def.Body, writer);
            writer.Write(" <a href=\"#fnref-"u8);
            writer.Write(def.Id);
            writer.Write("\">↩</a></li>\n"u8);
        }

        writer.Write("</ol>\n</section>\n"u8);
    }

    /// <summary>Linear-search for a definition by id.</summary>
    /// <param name="defs">Definition list.</param>
    /// <param name="id">Id bytes to find.</param>
    /// <returns>Index, or -1 when absent.</returns>
    private static int IndexOf(List<Definition> defs, ReadOnlySpan<byte> id)
    {
        for (var i = 0; i < defs.Count; i++)
        {
            if (id.SequenceEqual(defs[i].Id))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Writes <paramref name="value"/> as decimal ASCII bytes.</summary>
    /// <param name="value">Non-negative integer.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteInt(int value, IBufferWriter<byte> writer)
    {
        Span<byte> buf = stackalloc byte[16];
        if (!Utf8Formatter.TryFormat(value, buf, out var written))
        {
            return;
        }

        writer.Write(buf[..written]);
    }

    /// <summary>One footnote definition record.</summary>
    /// <param name="Id">UTF-8 footnote id bytes (the token between <c>[^</c> and <c>]</c>).</param>
    /// <param name="Body">UTF-8 definition body bytes.</param>
    private sealed record Definition(byte[] Id, byte[] Body);
}
