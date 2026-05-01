// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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
                && TryParseTable(source, i, out var headerEnd, out var separatorEnd, out var tableEnd))
            {
                EmitTable(source, i, headerEnd, separatorEnd, tableEnd, writer);
                i = tableEnd;
                continue;
            }

            var lineEnd = MarkdownCodeScanner.LineEnd(source, i);
            writer.Write(source[i..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Tries to detect a table header + separator + body block at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Candidate line-start offset.</param>
    /// <param name="headerEnd">End of the header line on success.</param>
    /// <param name="separatorEnd">End of the separator line on success.</param>
    /// <param name="tableEnd">End of the table block on success.</param>
    /// <returns>True when a table was detected.</returns>
    private static bool TryParseTable(ReadOnlySpan<byte> source, int offset, out int headerEnd, out int separatorEnd, out int tableEnd)
    {
        separatorEnd = 0;
        tableEnd = 0;

        if (!LineHasPipe(source, offset, out headerEnd))
        {
            return false;
        }

        if (!IsSeparatorLine(source, headerEnd, out separatorEnd))
        {
            return false;
        }

        var p = separatorEnd;
        while (p < source.Length && LineHasPipe(source, p, out var rowEnd))
        {
            p = rowEnd;
        }

        tableEnd = p;
        return true;
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

    /// <summary>Emits one <c>&lt;table&gt;</c> covering the parsed block.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="start">Start of the header line.</param>
    /// <param name="headerEnd">End of the header line.</param>
    /// <param name="separatorEnd">End of the separator line.</param>
    /// <param name="tableEnd">End of the table block.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitTable(ReadOnlySpan<byte> source, int start, int headerEnd, int separatorEnd, int tableEnd, IBufferWriter<byte> writer)
    {
        var aligns = ParseAlignments(TrimTerminator(source[headerEnd..separatorEnd]));
        writer.Write("\n<table>\n<thead>\n<tr>"u8);
        EmitRow(TrimTerminator(source[start..headerEnd]), aligns, "th"u8, writer);
        writer.Write("</tr>\n</thead>\n"u8);

        if (separatorEnd < tableEnd)
        {
            writer.Write("<tbody>\n"u8);
            var p = separatorEnd;
            while (p < tableEnd)
            {
                var rowEnd = MarkdownCodeScanner.LineEnd(source, p);
                writer.Write("<tr>"u8);
                EmitRow(TrimTerminator(source[p..rowEnd]), aligns, "td"u8, writer);
                writer.Write("</tr>\n"u8);
                p = rowEnd;
            }

            writer.Write("</tbody>\n"u8);
        }

        writer.Write("</table>\n\n"u8);
    }

    /// <summary>Parses alignment tokens out of a separator line.</summary>
    /// <param name="separator">UTF-8 separator line (no terminator).</param>
    /// <returns>Per-column alignment.</returns>
    private static Align[] ParseAlignments(ReadOnlySpan<byte> separator)
    {
        var cells = SplitCells(separator);
        var aligns = new Align[cells.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            var cell = Trim(cells[i]);
            var startsColon = cell.Length > 0 && cell[0] == (byte)':';
            var endsColon = cell.Length > 0 && cell[^1] == (byte)':';
            aligns[i] = (startsColon, endsColon) switch
            {
                (true, true) => Align.Center,
                (false, true) => Align.Right,
                (true, false) => Align.Left,
                _ => Align.None,
            };
        }

        return aligns;
    }

    /// <summary>Splits a row into its cells.</summary>
    /// <param name="row">UTF-8 row bytes (no terminator).</param>
    /// <returns>Cell slices in column order.</returns>
    private static byte[][] SplitCells(ReadOnlySpan<byte> row)
    {
        // Strip optional leading and trailing pipes.
        var trimmed = Trim(row);
        if (trimmed.Length > 0 && trimmed[0] == (byte)'|')
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length > 0 && trimmed[^1] == (byte)'|')
        {
            trimmed = trimmed[..^1];
        }

        var count = 1;
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] == (byte)'|')
            {
                count++;
            }
        }

        var cells = new byte[count][];
        var idx = 0;
        var cellStart = 0;
        for (var i = 0; i < trimmed.Length; i++)
        {
            if (trimmed[i] != (byte)'|')
            {
                continue;
            }

            cells[idx++] = [.. trimmed[cellStart..i]];
            cellStart = i + 1;
        }

        cells[idx] = [.. trimmed[cellStart..]];
        return cells;
    }

    /// <summary>Emits the cells of one row.</summary>
    /// <param name="row">UTF-8 row bytes (no terminator).</param>
    /// <param name="aligns">Column alignments.</param>
    /// <param name="tag">Cell element name (<c>th</c> or <c>td</c>).</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitRow(ReadOnlySpan<byte> row, Align[] aligns, ReadOnlySpan<byte> tag, IBufferWriter<byte> writer)
    {
        var cells = SplitCells(row);
        for (var i = 0; i < cells.Length; i++)
        {
            writer.Write("<"u8);
            writer.Write(tag);
            var align = i < aligns.Length ? aligns[i] : Align.None;
            WriteAlignAttr(align, writer);
            writer.Write(">"u8);
            HtmlEscaper.Escape(Trim(cells[i]), writer);
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
        while (start < end && (value[start] == (byte)' ' || value[start] == (byte)'\t'))
        {
            start++;
        }

        while (end > start && (value[end - 1] == (byte)' ' || value[end - 1] == (byte)'\t'))
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
}
