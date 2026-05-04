// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.MarkdownExtensions.Internal;

namespace NuStreamDocs.MarkdownExtensions.Tables;

/// <summary>
/// Stateless UTF-8 GFM-table rewriter. Detects a pipe-delimited
/// header line followed by a separator line of the form
/// <c>| --- | :--- | :---: |</c> and any number of body rows; emits
/// the matching <c>&lt;table&gt;</c> HTML and consumes the bytes.
/// </summary>
internal static class TablesRewriter
{
    /// <summary>Stack-buffer cap for columns parsed out of one table.</summary>
    /// <remarks>Real-world GFM tables stay well under this; rows with more cells silently fold the overflow into the last cell.</remarks>
    private const int MaxColumns = 32;

    /// <summary>Column alignment.</summary>
    private enum Align
    {
        /// <summary>No explicit alignment.</summary>
        None,

        /// <summary>Left aligned.</summary>
        Left,

        /// <summary>Right aligned.</summary>
        Right,

        /// <summary>Centre aligned.</summary>
        Center,
    }

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i)
                && TryEmitTable(source, i, writer, out var consumed))
            {
                i = consumed;
                continue;
            }

            var lineEnd = MarkdownCodeScanner.LineEnd(source, i);
            writer.Write(source[i..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Detects and emits one <c>&lt;table&gt;</c> block starting at <paramref name="start"/>; the body rows are walked exactly once.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="start">Candidate line-start offset.</param>
    /// <param name="writer">UTF-8 sink; only written to once header + separator are confirmed.</param>
    /// <param name="end">Set to the byte after the consumed table block on success.</param>
    /// <returns>True when a table was emitted.</returns>
    private static bool TryEmitTable(ReadOnlySpan<byte> source, int start, IBufferWriter<byte> writer, out int end)
    {
        end = start;
        if (!LineHasPipe(source, start, out var headerEnd) || !IsSeparatorLine(source, headerEnd, out var separatorEnd))
        {
            return false;
        }

        Span<Align> alignBuffer = stackalloc Align[MaxColumns];
        var alignCount = ParseAlignments(TrimTerminator(source[headerEnd..separatorEnd]), alignBuffer);
        var aligns = alignBuffer[..alignCount];

        writer.Write("\n<table>\n<thead>\n<tr>"u8);
        EmitRow(TrimTerminator(source[start..headerEnd]), aligns, "th"u8, writer);
        writer.Write("</tr>\n</thead>\n"u8);

        end = EmitBodyRows(source, separatorEnd, aligns, writer);
        writer.Write("</table>\n\n"u8);
        return true;
    }

    /// <summary>Walks body rows from <paramref name="start"/> and emits them; opens/closes <c>&lt;tbody&gt;</c> only when at least one row is present.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="start">Body-rows start offset (just past the separator line).</param>
    /// <param name="aligns">Column alignments parsed from the separator.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Offset just past the last consumed body row.</returns>
    private static int EmitBodyRows(ReadOnlySpan<byte> source, int start, in ReadOnlySpan<Align> aligns, IBufferWriter<byte> writer)
    {
        var p = start;
        var bodyOpen = false;
        while (p < source.Length && LineHasPipe(source, p, out var rowEnd))
        {
            if (!bodyOpen)
            {
                writer.Write("<tbody>\n"u8);
                bodyOpen = true;
            }

            writer.Write("<tr>"u8);
            EmitRow(TrimTerminator(source[p..rowEnd]), aligns, "td"u8, writer);
            writer.Write("</tr>\n"u8);
            p = rowEnd;
        }

        if (bodyOpen)
        {
            writer.Write("</tbody>\n"u8);
        }

        return p;
    }

    /// <summary>Returns true when the line at <paramref name="offset"/> contains at least one <c>|</c> character.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Line-start offset.</param>
    /// <param name="lineEnd">Set to the exclusive line end on success.</param>
    /// <returns>True when the line has a pipe and is non-blank.</returns>
    private static bool LineHasPipe(ReadOnlySpan<byte> source, int offset, out int lineEnd)
    {
        lineEnd = MarkdownCodeScanner.LineEnd(source, offset);
        var line = source[offset..lineEnd];
        if (IndentedBlockScanner.IsBlankLine(line))
        {
            return false;
        }

        return line.IndexOf((byte)'|') >= 0;
    }

    /// <summary>Returns true when the line at <paramref name="offset"/> is a GFM separator (<c>| --- | :---: |</c>).</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Line-start offset.</param>
    /// <param name="lineEnd">Set to the exclusive line end on success.</param>
    /// <returns>True when the line is a separator.</returns>
    private static bool IsSeparatorLine(ReadOnlySpan<byte> source, int offset, out int lineEnd)
    {
        lineEnd = MarkdownCodeScanner.LineEnd(source, offset);
        var line = TrimTerminator(source[offset..lineEnd]);
        if (line.IndexOf((byte)'|') < 0)
        {
            return false;
        }

        for (var i = 0; i < line.Length; i++)
        {
            var b = line[i];
            if (b is not ((byte)'|' or (byte)'-' or (byte)':' or (byte)' ' or (byte)'\t'))
            {
                return false;
            }
        }

        return line.IndexOf((byte)'-') >= 0;
    }

    /// <summary>Parses alignment tokens out of a separator line into <paramref name="aligns"/>.</summary>
    /// <param name="separator">UTF-8 separator line (no terminator).</param>
    /// <param name="aligns">Output alignment buffer.</param>
    /// <returns>Number of alignments populated.</returns>
    private static int ParseAlignments(ReadOnlySpan<byte> separator, in Span<Align> aligns)
    {
        Span<CellRange> cellBuffer = stackalloc CellRange[MaxColumns];
        var count = SplitCellsInto(separator, cellBuffer);
        var cap = Math.Min(count, aligns.Length);
        for (var i = 0; i < cap; i++)
        {
            var cell = Trim(separator.Slice(cellBuffer[i].Start, cellBuffer[i].Length));
            var startsColon = cell.Length > 0 && cell[0] is (byte)':';
            var endsColon = cell.Length > 0 && cell[^1] is (byte)':';
            aligns[i] = (startsColon, endsColon) switch
            {
                (true, true) => Align.Center,
                (false, true) => Align.Right,
                (true, false) => Align.Left,
                _ => Align.None,
            };
        }

        return cap;
    }

    /// <summary>Splits <paramref name="row"/> into cell ranges, writing into <paramref name="cells"/>.</summary>
    /// <param name="row">UTF-8 row bytes (no terminator).</param>
    /// <param name="cells">Output range buffer.</param>
    /// <returns>Number of cells populated; capped at <paramref name="cells"/>.Length with the overflow rolled into the final range.</returns>
    private static int SplitCellsInto(ReadOnlySpan<byte> row, in Span<CellRange> cells)
    {
        TrimRowBounds(row, out var leading, out var trailing);
        return CollectCellRanges(row, leading, trailing, cells);
    }

    /// <summary>Computes the inner row bounds after stripping leading/trailing horizontal whitespace and optional outer pipes.</summary>
    /// <param name="row">UTF-8 row bytes.</param>
    /// <param name="leading">Inclusive inner start.</param>
    /// <param name="trailing">Exclusive inner end.</param>
    private static void TrimRowBounds(ReadOnlySpan<byte> row, out int leading, out int trailing)
    {
        leading = SkipLeadingWhitespace(row, 0, row.Length);
        trailing = SkipTrailingWhitespace(row, leading, row.Length);
        leading = StripLeadingPipe(row, leading, trailing);
        trailing = StripTrailingPipe(row, leading, trailing);
    }

    /// <summary>Advances <paramref name="from"/> past leading horizontal whitespace within [from, to).</summary>
    /// <param name="row">UTF-8 row bytes.</param>
    /// <param name="from">Inclusive start.</param>
    /// <param name="to">Exclusive end.</param>
    /// <returns>First non-whitespace offset, or <paramref name="to"/> when the range is all whitespace.</returns>
    private static int SkipLeadingWhitespace(ReadOnlySpan<byte> row, int from, int to)
    {
        while (from < to && IsHorizontalWhitespace(row[from]))
        {
            from++;
        }

        return from;
    }

    /// <summary>Walks <paramref name="to"/> back past trailing horizontal whitespace within [from, to).</summary>
    /// <param name="row">UTF-8 row bytes.</param>
    /// <param name="from">Inclusive start.</param>
    /// <param name="to">Exclusive end.</param>
    /// <returns>Trimmed exclusive end.</returns>
    private static int SkipTrailingWhitespace(ReadOnlySpan<byte> row, int from, int to)
    {
        while (to > from && IsHorizontalWhitespace(row[to - 1]))
        {
            to--;
        }

        return to;
    }

    /// <summary>Strips one optional <c>|</c> from the leading edge of [from, to).</summary>
    /// <param name="row">UTF-8 row bytes.</param>
    /// <param name="from">Inclusive start.</param>
    /// <param name="to">Exclusive end.</param>
    /// <returns>Updated leading offset.</returns>
    private static int StripLeadingPipe(ReadOnlySpan<byte> row, int from, int to) =>
        from < to && row[from] is (byte)'|' ? from + 1 : from;

    /// <summary>Strips one optional <c>|</c> from the trailing edge of [from, to).</summary>
    /// <param name="row">UTF-8 row bytes.</param>
    /// <param name="from">Inclusive start.</param>
    /// <param name="to">Exclusive end.</param>
    /// <returns>Updated trailing offset.</returns>
    private static int StripTrailingPipe(ReadOnlySpan<byte> row, int from, int to) =>
        to > from && row[to - 1] is (byte)'|' ? to - 1 : to;

    /// <summary>Records every <c>|</c>-bounded cell between <paramref name="leading"/> and <paramref name="trailing"/> as a range into <paramref name="cells"/>.</summary>
    /// <param name="row">UTF-8 row bytes.</param>
    /// <param name="leading">Inclusive inner start.</param>
    /// <param name="trailing">Exclusive inner end.</param>
    /// <param name="cells">Output range buffer.</param>
    /// <returns>Number of cells populated.</returns>
    private static int CollectCellRanges(ReadOnlySpan<byte> row, int leading, int trailing, in Span<CellRange> cells)
    {
        var count = 0;
        var cellStart = leading;
        for (var i = leading; i < trailing; i++)
        {
            if (row[i] is not (byte)'|')
            {
                continue;
            }

            if (count >= cells.Length - 1)
            {
                break;
            }

            cells[count++] = new(cellStart, i - cellStart);
            cellStart = i + 1;
        }

        if (count < cells.Length)
        {
            cells[count++] = new(cellStart, trailing - cellStart);
        }

        return count;
    }

    /// <summary>True for ASCII space or tab; tables don't span lines so CR/LF are excluded.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for space or tab.</returns>
    private static bool IsHorizontalWhitespace(byte b) => b is (byte)' ' or (byte)'\t';

    /// <summary>Emits the cells of one row.</summary>
    /// <param name="row">UTF-8 row bytes (no terminator).</param>
    /// <param name="aligns">Column alignments.</param>
    /// <param name="tag">Cell element name (<c>th</c> or <c>td</c>).</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitRow(ReadOnlySpan<byte> row, in ReadOnlySpan<Align> aligns, ReadOnlySpan<byte> tag, IBufferWriter<byte> writer)
    {
        Span<CellRange> cellBuffer = stackalloc CellRange[MaxColumns];
        var count = SplitCellsInto(row, cellBuffer);
        for (var i = 0; i < count; i++)
        {
            writer.Write("<"u8);
            writer.Write(tag);
            var align = i < aligns.Length ? aligns[i] : Align.None;
            WriteAlignAttr(align, writer);
            writer.Write(">"u8);

            // Cells render as inline markdown — `[label](href)`, emphasis, code spans, autolinks —
            // so links inside table cells resolve to anchor tags instead of staying literal.
            InlineRenderer.Render(Trim(row.Slice(cellBuffer[i].Start, cellBuffer[i].Length)), writer);
            writer.Write("</"u8);
            writer.Write(tag);
            writer.Write(">"u8);
        }
    }

    /// <summary>Writes the <c>style="text-align:…"</c> attribute when <paramref name="align"/> is non-default.</summary>
    /// <param name="align">Column alignment.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteAlignAttr(Align align, IBufferWriter<byte> writer)
    {
        var attr = align switch
        {
            Align.Left => " style=\"text-align:left\""u8,
            Align.Right => " style=\"text-align:right\""u8,
            Align.Center => " style=\"text-align:center\""u8,
            _ => default,
        };

        writer.Write(attr);
    }

    /// <summary>Strips leading/trailing horizontal whitespace.</summary>
    /// <param name="value">UTF-8 input.</param>
    /// <returns>Trimmed slice.</returns>
    private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
    {
        var start = 0;
        var end = value.Length;
        while (start < end && IsHorizontalWhitespace(value[start]))
        {
            start++;
        }

        while (end > start && IsHorizontalWhitespace(value[end - 1]))
        {
            end--;
        }

        return value[start..end];
    }

    /// <summary>Strips a trailing CRLF or LF.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>Slice without the terminator.</returns>
    private static ReadOnlySpan<byte> TrimTerminator(ReadOnlySpan<byte> line)
    {
        if (line.Length > 0 && line[^1] == (byte)'\n')
        {
            line = line[..^1];
        }

        if (line.Length > 0 && line[^1] == (byte)'\r')
        {
            line = line[..^1];
        }

        return line;
    }

    /// <summary>One cell's slice into the row span.</summary>
    /// <param name="Start">Inclusive start offset.</param>
    /// <param name="Length">Slice length in bytes.</param>
    private readonly record struct CellRange(int Start, int Length);
}
