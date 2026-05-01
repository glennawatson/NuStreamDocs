// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.MarkdownExtensions.Internal;

namespace NuStreamDocs.MarkdownExtensions.Admonitions;

/// <summary>
/// Stateless UTF-8 admonition rewriter. Scans <c>!!! type "title"</c>
/// openers and the indented body that follows, emits the matching
/// <c>&lt;div class="admonition type"&gt;</c> HTML block, and copies
/// every other byte through unchanged.
/// </summary>
internal static class AdmonitionRewriter
{
    /// <summary>Gets the opener marker: <c>!!! </c> with the trailing space.</summary>
    private static ReadOnlySpan<byte> Opener => "!!! "u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i)
                && TryParseOpener(source, i, out var typeStart, out var typeLen, out var titleStart, out var titleLen, out var headerEnd))
            {
                var bodyEnd = IndentedBlockScanner.ConsumeBody(source, headerEnd);
                EmitAdmonition(
                    source.Slice(typeStart, typeLen),
                    titleLen is 0 ? default : source.Slice(titleStart, titleLen),
                    source[headerEnd..bodyEnd],
                    writer);
                i = bodyEnd;
                continue;
            }

            var lineEnd = MarkdownCodeScanner.LineEnd(source, i);
            writer.Write(source[i..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Tries to parse an admonition opener starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="offset">Candidate line-start offset.</param>
    /// <param name="typeStart">Set to the offset of the type token on success.</param>
    /// <param name="typeLen">Set to the length of the type token on success.</param>
    /// <param name="titleStart">Set to the offset of the title token on success (zero when absent).</param>
    /// <param name="titleLen">Set to the length of the title token on success (zero when absent).</param>
    /// <param name="headerEnd">Set to the byte offset just past the opener line's terminator.</param>
    /// <returns>True when <paramref name="offset"/> begins a valid opener.</returns>
    private static bool TryParseOpener(
        ReadOnlySpan<byte> source,
        int offset,
        out int typeStart,
        out int typeLen,
        out int titleStart,
        out int titleLen,
        out int headerEnd)
    {
        typeStart = 0;
        typeLen = 0;
        titleStart = 0;
        titleLen = 0;
        headerEnd = 0;

        if (offset + Opener.Length > source.Length || !source[offset..].StartsWith(Opener))
        {
            return false;
        }

        return OpenerLineParser.TryParseTypeAndTitle(
            source,
            offset + Opener.Length,
            out typeStart,
            out typeLen,
            out titleStart,
            out titleLen,
            out headerEnd);
    }

    /// <summary>Emits one admonition <c>&lt;div&gt;</c> block.</summary>
    /// <param name="type">UTF-8 type token.</param>
    /// <param name="title">UTF-8 title bytes; empty span when no title was specified.</param>
    /// <param name="body">UTF-8 body bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitAdmonition(ReadOnlySpan<byte> type, ReadOnlySpan<byte> title, ReadOnlySpan<byte> body, IBufferWriter<byte> writer)
    {
        writer.Write("\n<div class=\"admonition "u8);
        writer.Write(type);
        writer.Write("\">\n<p class=\"admonition-title\">"u8);
        if (title.Length is 0)
        {
            OpenerLineParser.WriteTitleCase(type, writer);
        }
        else
        {
            HtmlEscaper.Escape(title, writer);
        }

        writer.Write("</p>\n\n"u8);
        IndentedBlockScanner.WriteDeindented(body, writer);
        writer.Write("\n</div>\n\n"u8);
    }
}
