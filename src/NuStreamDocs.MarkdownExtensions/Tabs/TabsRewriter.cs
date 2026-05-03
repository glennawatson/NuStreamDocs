// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.MarkdownExtensions.Internal;

namespace NuStreamDocs.MarkdownExtensions.Tabs;

/// <summary>
/// Stateless UTF-8 content-tabs rewriter. Groups consecutive
/// <c>=== "Title"</c> openers (with their indented bodies) into one
/// tabbed-set <c>&lt;div&gt;</c>. The first tab in each set is
/// checked.
/// </summary>
internal static class TabsRewriter
{
    /// <summary>Process-wide counter so tab radio-input names are unique across pages.</summary>
    private static int _setCounter;

    /// <summary>Gets the opener marker.</summary>
    private static ReadOnlySpan<byte> Opener => "=== "u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i)
                && TryParseOpener(source, i, out var titleStart, out var titleLen, out var headerEnd))
            {
                i = EmitSet(source, i, titleStart, titleLen, headerEnd, writer);
                continue;
            }

            var lineEnd = MarkdownCodeScanner.LineEnd(source, i);
            writer.Write(source[i..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Emits one tabbed-set covering every contiguous opener starting at <paramref name="setStart"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="setStart">Offset of the first opener in the set.</param>
    /// <param name="firstTitleStart">Title-token offset of the first opener.</param>
    /// <param name="firstTitleLen">Title-token length of the first opener.</param>
    /// <param name="firstHeaderEnd">Offset just past the first opener's terminator.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>The exclusive end offset of the rewritten set.</returns>
    private static int EmitSet(
        ReadOnlySpan<byte> source,
        int setStart,
        int firstTitleStart,
        int firstTitleLen,
        int firstHeaderEnd,
        IBufferWriter<byte> writer)
    {
        var setId = Interlocked.Increment(ref _setCounter);
        writer.Write("\n<div class=\"tabbed-set\">\n"u8);

        var cursor = setStart;
        var titleStart = firstTitleStart;
        var titleLen = firstTitleLen;
        var headerEnd = firstHeaderEnd;
        var tabIndex = 0;

        var more = true;
        while (more)
        {
            var bodyEnd = IndentedBlockScanner.ConsumeBody(source, headerEnd);
            EmitTab(setId, tabIndex, source.Slice(titleStart, titleLen), source[headerEnd..bodyEnd], writer);
            cursor = bodyEnd;
            tabIndex++;

            // ConsumeBody trims trailing blank lines from its return value, so a `===` opener
            // separated from the previous body by a blank line lands at the start of a `\n`-only
            // line rather than the opener bytes. Skip blanks before re-probing.
            var probe = SkipBlankLines(source, cursor);
            more = MarkdownCodeScanner.AtLineStart(source, probe)
                && TryParseOpener(source, probe, out titleStart, out titleLen, out headerEnd);
            if (more)
            {
                cursor = probe;
            }
        }

        writer.Write("</div>\n\n"u8);
        return cursor;
    }

    /// <summary>Tries to parse a tabs opener starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="offset">Candidate line-start offset.</param>
    /// <param name="titleStart">Title-token offset on success.</param>
    /// <param name="titleLen">Title-token length on success.</param>
    /// <param name="headerEnd">Offset just past the opener's terminator.</param>
    /// <returns>True when <paramref name="offset"/> begins a valid opener.</returns>
    private static bool TryParseOpener(
        ReadOnlySpan<byte> source,
        int offset,
        out int titleStart,
        out int titleLen,
        out int headerEnd)
    {
        titleStart = 0;
        titleLen = 0;
        headerEnd = 0;

        if (offset + Opener.Length > source.Length || !source[offset..].StartsWith(Opener))
        {
            return false;
        }

        var p = offset + Opener.Length;
        if (p >= source.Length || source[p] != (byte)'"')
        {
            return false;
        }

        if (!OpenerLineParser.TryParseTitle(source, ref p, out titleStart, out titleLen))
        {
            return false;
        }

        p = OpenerLineParser.ScanWhile(source, p, OpenerLineParser.IsTrailingHeaderByte);
        if (p < source.Length && source[p] != (byte)'\n')
        {
            return false;
        }

        headerEnd = p < source.Length ? p + 1 : p;
        return true;
    }

    /// <summary>Emits one tab inside a tabbed-set.</summary>
    /// <param name="setId">Set-unique id; encodes into the radio name.</param>
    /// <param name="tabIndex">Zero-based tab index inside the set.</param>
    /// <param name="title">UTF-8 title bytes.</param>
    /// <param name="body">UTF-8 body bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitTab(int setId, int tabIndex, ReadOnlySpan<byte> title, ReadOnlySpan<byte> body, IBufferWriter<byte> writer)
    {
        writer.Write("<input type=\"radio\" name=\"__tabbed_"u8);
        WriteInt(setId, writer);
        writer.Write("\" id=\"__tabbed_"u8);
        WriteInt(setId, writer);
        writer.Write("_"u8);
        WriteInt(tabIndex, writer);
        writer.Write("\""u8);
        if (tabIndex is 0)
        {
            writer.Write(" checked"u8);
        }

        writer.Write(">\n<label for=\"__tabbed_"u8);
        WriteInt(setId, writer);
        writer.Write("_"u8);
        WriteInt(tabIndex, writer);
        writer.Write("\">"u8);
        HtmlEscaper.Escape(title, writer);
        writer.Write("</label>\n<div class=\"tabbed-content\">\n\n"u8);
        IndentedBlockScanner.WriteDeindented(body, writer);
        writer.Write("\n</div>\n"u8);
    }

    /// <summary>Advances <paramref name="offset"/> past any run of blank (whitespace-only) lines starting at a line boundary.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Cursor; expected to be at the start of a line.</param>
    /// <returns>Offset of the first non-blank line at or after <paramref name="offset"/>.</returns>
    private static int SkipBlankLines(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length)
        {
            var rel = source[p..].IndexOf((byte)'\n');
            var lineEnd = rel < 0 ? source.Length : p + rel + 1;
            if (!IndentedBlockScanner.IsBlankLine(source[p..lineEnd]))
            {
                return p;
            }

            p = lineEnd;
        }

        return p;
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
}
