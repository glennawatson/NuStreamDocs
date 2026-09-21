// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown;

/// <summary>Expands tabs in the leading indentation of each line, and after block-quote markers, to spaces so block structure sees consistent columns.</summary>
internal static class TabExpander
{
    /// <summary>Column width of one tab stop.</summary>
    private const int TabStop = 4;

    /// <summary>Tab byte.</summary>
    private const byte Tab = (byte)'\t';

    /// <summary>Space byte.</summary>
    private const byte Space = (byte)' ';

    /// <summary>Block-quote marker byte; tabs after it are expanded against the columns it occupies.</summary>
    private const byte QuoteMarker = (byte)'>';

    /// <summary>Line-feed byte.</summary>
    private const byte Lf = (byte)'\n';

    /// <summary>True when <paramref name="source"/> holds any tab byte and so may need expansion.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when a tab is present.</returns>
    internal static bool MayNeedExpansion(ReadOnlySpan<byte> source) => source.IndexOf(Tab) >= 0;

    /// <summary>Writes <paramref name="source"/> to <paramref name="destination"/> with every tab in a line's leading whitespace replaced by spaces up to the next tab stop.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <param name="destination">Receives the expanded bytes; at least <see cref="MeasureExpanded"/> bytes long. Tabs after the leading whitespace and quote markers are kept.</param>
    internal static void Expand(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        var written = 0;
        var pos = 0;
        while (pos < source.Length)
        {
            var indentEnd = SkipIndent(source, pos, out _);
            written += ExpandIndent(source[pos..indentEnd], destination[written..]);

            var next = NextLineStart(source, indentEnd);
            source[indentEnd..next].CopyTo(destination[written..]);
            written += next - indentEnd;
            pos = next;
        }
    }

    /// <summary>Computes the byte length of the expanded output.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>Expanded length in bytes.</returns>
    internal static int MeasureExpanded(ReadOnlySpan<byte> source)
    {
        var total = 0;
        var pos = 0;
        while (pos < source.Length)
        {
            var indentEnd = SkipIndent(source, pos, out var columns);
            var next = NextLineStart(source, indentEnd);
            total += columns + (next - indentEnd);
            pos = next;
        }

        return total;
    }

    /// <summary>Walks the leading run of spaces, tabs and block-quote markers of the line starting at <paramref name="lineStart"/>.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <param name="lineStart">Index of the first byte of the line.</param>
    /// <param name="columns">Width of the run in columns after tab expansion.</param>
    /// <returns>Index of the first byte after the run.</returns>
    private static int SkipIndent(ReadOnlySpan<byte> source, int lineStart, out int columns)
    {
        columns = 0;
        var i = lineStart;
        while (i < source.Length && source[i] is Space or Tab or QuoteMarker)
        {
            columns += source[i] is Tab ? TabStop - (columns % TabStop) : 1;
            i++;
        }

        return i;
    }

    /// <summary>Copies a leading space/tab/quote-marker run into <paramref name="destination"/> with each tab replaced by spaces up to the next tab stop.</summary>
    /// <param name="indent">Leading run of one line.</param>
    /// <param name="destination">Receives the expanded run.</param>
    /// <returns>Number of bytes written.</returns>
    private static int ExpandIndent(ReadOnlySpan<byte> indent, Span<byte> destination)
    {
        var written = 0;
        for (var i = 0; i < indent.Length; i++)
        {
            if (indent[i] is not Tab)
            {
                destination[written] = indent[i];
                written++;
                continue;
            }

            var width = TabStop - (written % TabStop);
            destination.Slice(written, width).Fill(Space);
            written += width;
        }

        return written;
    }

    /// <summary>Returns the index of the first byte of the line after the one containing <paramref name="from"/>.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <param name="from">Index inside the current line.</param>
    /// <returns>Start of the next line, or the source length on the last line.</returns>
    private static int NextLineStart(ReadOnlySpan<byte> source, int from)
    {
        var lf = source[from..].IndexOf(Lf);
        return lf < 0 ? source.Length : from + lf + 1;
    }
}
