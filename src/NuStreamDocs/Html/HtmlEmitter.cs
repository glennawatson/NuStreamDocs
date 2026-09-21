// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Html;

/// <summary>Renders <see cref="BlockSpan"/> sequences to UTF-8 HTML.</summary>
public static class HtmlEmitter
{
    /// <summary>Lowest CommonMark ATX heading level.</summary>
    private const int MinHeadingLevel = 1;

    /// <summary>Highest CommonMark ATX heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>Maximum digits read from an ordered-list marker; CommonMark caps markers at nine so the value fits an <see cref="int"/>.</summary>
    private const int MaxOrderedDigits = 9;

    /// <summary>Radix of the ordered-list marker digits.</summary>
    private const int DecimalBase = 10;

    /// <summary>Buffer size that fits any formatted <see cref="int"/>.</summary>
    private const int MaxFormattedIntLength = 11;

    /// <summary>Initial block capacity for a list item body scanned on its own.</summary>
    private const int InitialNestedBlockCapacity = 8;

    /// <summary>Bytes that can begin a block other than a paragraph (digits are checked separately).</summary>
    private static readonly SearchValues<byte> BlockMarkerBytes = SearchValues.Create("#>-*+_`~<"u8);

    /// <summary>Open-tag UTF-8 literals indexed by heading level (index 0 unused).</summary>
    private static readonly byte[][] OpenTags =
    [
        [.. "<h?>"u8],
        [.. "<h1>"u8],
        [.. "<h2>"u8],
        [.. "<h3>"u8],
        [.. "<h4>"u8],
        [.. "<h5>"u8],
        [.. "<h6>"u8]
    ];

    /// <summary>Close-tag UTF-8 literals indexed by heading level.</summary>
    private static readonly byte[][] CloseTags =
    [
        [.. "</h?>\n"u8],
        [.. "</h1>\n"u8],
        [.. "</h2>\n"u8],
        [.. "</h3>\n"u8],
        [.. "</h4>\n"u8],
        [.. "</h5>\n"u8],
        [.. "</h6>\n"u8]
    ];

    /// <summary>Gets the paragraph terminator emitted after block content.</summary>
    private static ReadOnlySpan<byte> ParagraphClose => "</p>\n"u8;

    /// <summary>Renders <paramref name="blocks"/> against <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">Original UTF-8 source the block descriptors index into.</param>
    /// <param name="blocks">Block descriptors emitted by <see cref="BlockScanner"/>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Emit(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        IBufferWriter<byte> writer) =>
        EmitBlocks(source, blocks, false, writer);

    /// <summary>Pulls the info-string (language tag) from a fenced-code opener line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="opener">Opener block.</param>
    /// <returns>Trimmed info string bytes; empty when none.</returns>
    public static ReadOnlySpan<byte> ExtractInfoString(ReadOnlySpan<byte> source, in BlockSpan opener)
    {
        var rest = ExtractFenceInfoLine(source, opener);
        var space = rest.IndexOf((byte)' ');
        return space < 0 ? rest : rest[..space];
    }

    /// <summary>Returns the trailing info-string content (after the language word) for a fenced-code opener — the per-block attribute payload.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="opener">Opener block.</param>
    /// <returns>Trailing info string bytes; empty when there is only a language word or no info string.</returns>
    public static ReadOnlySpan<byte> ExtractInfoStringTail(ReadOnlySpan<byte> source, in BlockSpan opener)
    {
        var rest = ExtractFenceInfoLine(source, opener);
        var space = rest.IndexOf((byte)' ');
        return space < 0 ? [] : rest[(space + 1)..].TrimStart((byte)' ');
    }

    /// <summary>Renders every block in <paramref name="blocks"/>.</summary>
    /// <param name="source">UTF-8 source the block descriptors index into.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="tight">True to emit paragraph text without <c>&lt;p&gt;</c> wrappers, as inside a tight list item.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitBlocks(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        bool tight,
        IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < blocks.Length)
        {
            i = EmitOne(source, blocks, i, tight, writer);
        }
    }

    /// <summary>Writes the newline that separates unwrapped paragraph text from a following block.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="next">Index just past the paragraph.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitTightBreak(in ReadOnlySpan<BlockSpan> blocks, int next, IBufferWriter<byte> writer)
    {
        var i = next;
        while (i < blocks.Length && blocks[i].Kind is BlockKind.Blank)
        {
            i++;
        }

        if (i < blocks.Length)
        {
            Write("\n"u8, writer);
        }
    }

    /// <summary>Emits the block at <paramref name="index"/> and returns the cursor for the next iteration.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="blocks">All block descriptors.</param>
    /// <param name="index">Cursor for the current block.</param>
    /// <param name="tight">True to emit paragraph text without <c>&lt;p&gt;</c> wrappers.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Next-iteration cursor; always strictly greater than <paramref name="index"/>.</returns>
    private static int EmitOne(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int index,
        bool tight,
        IBufferWriter<byte> writer)
    {
        var block = blocks[index];
        switch (block.Kind)
        {
            case BlockKind.AtxHeading:
                {
                    EmitHeading(source, block, writer);
                    return index + 1;
                }

            case BlockKind.Paragraph:
                {
                    return EmitParagraphRun(source, blocks, index, tight, writer);
                }

            case BlockKind.FencedCode:
                {
                    return EmitFencedCode(source, blocks, index, writer) + 1;
                }

            case BlockKind.FencedCodeContent:
                {
                    // Reached only when fences are unbalanced (no opener seen yet); treat as paragraph
                    // so the content still surfaces in the output.
                    EmitParagraph(source, blocks, index, tight, writer);
                    return index + 1;
                }

            default:
                {
                    return EmitDispatch(source, blocks, index, tight, writer) + 1;
                }
        }
    }

    /// <summary>Returns the trimmed body of the fence opener line, with the leading fence markers stripped.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="opener">Opener block.</param>
    /// <returns>Trimmed line content.</returns>
    private static ReadOnlySpan<byte> ExtractFenceInfoLine(ReadOnlySpan<byte> source, in BlockSpan opener)
    {
        var line = source.Slice(opener.Start, opener.Length);
        var marker = !line.IsEmpty && line[0] == (byte)'~' ? (byte)'~' : (byte)'`';
        var i = 0;
        while (i < line.Length && line[i] == marker)
        {
            i++;
        }

        return line[i..].TrimStart((byte)' ').TrimEnd((byte)' ');
    }

    /// <summary>Writes an <c>&lt;hN&gt;</c> element using the block's level.</summary>
    /// <param name="source">Original UTF-8 source buffer.</param>
    /// <param name="block">Heading block descriptor.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitHeading(ReadOnlySpan<byte> source, in BlockSpan block, IBufferWriter<byte> writer)
    {
        var level = Math.Clamp(block.Level, MinHeadingLevel, MaxHeadingLevel);

        Write(OpenTags[level], writer);

        var inner = source.Slice(block.Start + level, block.Length - level).TrimStart((byte)' ');
        InlineRenderer.Render(inner, writer);

        Write(CloseTags[level], writer);
    }

    /// <summary>Writes a <c>&lt;pre&gt;&lt;code&gt;</c> block, consuming every <see cref="BlockKind.FencedCodeContent"/> line until the matching closer.</summary>
    /// <param name="source">Original UTF-8 source buffer.</param>
    /// <param name="blocks">Full block descriptor span.</param>
    /// <param name="openerIndex">Index of the opener <see cref="BlockKind.FencedCode"/> block.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the closing fence (or last consumed block when the source had no closer).</returns>
    private static int EmitFencedCode(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int openerIndex,
        IBufferWriter<byte> writer)
    {
        var opener = blocks[openerIndex];
        var info = ExtractInfoString(source, opener);
        var infoTail = ExtractInfoStringTail(source, opener);

        Write("<pre><code"u8, writer);
        if (!info.IsEmpty)
        {
            Write(" class=\"language-"u8, writer);
            Write(info, writer);
            Write("\""u8, writer);
        }

        if (!infoTail.IsEmpty)
        {
            Write(" data-info=\""u8, writer);
            HtmlEscape.EscapeText(infoTail, writer);
            Write("\""u8, writer);
        }

        Write(">"u8, writer);

        var closerIndex = openerIndex;
        for (var j = openerIndex + 1; j < blocks.Length; j++)
        {
            if (blocks[j].Kind == BlockKind.FencedCodeContent)
            {
                EmitCodeContentLine(source, blocks[j], writer);
                continue;
            }

            if (blocks[j].Kind == BlockKind.FencedCode)
            {
                closerIndex = j;
                break;
            }

            // Unbalanced — first non-fence block stops the run; rewind so
            // the outer loop renders it normally on the next iteration.
            closerIndex = j - 1;
            break;
        }

        Write("</code></pre>\n"u8, writer);
        return closerIndex;
    }

    /// <summary>Writes one fenced-code body line, HTML-escaped, with a trailing newline.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="block">Content block.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitCodeContentLine(ReadOnlySpan<byte> source, in BlockSpan block, IBufferWriter<byte> writer)
    {
        var line = source.Slice(block.Start, block.Length);
        HtmlEscape.EscapeText(line, writer);
        Write("\n"u8, writer);
    }

    /// <summary>Writes one line of an HTML block verbatim — no inline render, no escape.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="block">Line block descriptor.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitHtmlBlockLine(ReadOnlySpan<byte> source, in BlockSpan block, IBufferWriter<byte> writer)
    {
        var line = source.Slice(block.Start, block.Length);
        Write(line, writer);
        Write("\n"u8, writer);
    }

    /// <summary>Writes a paragraph-wrapped, HTML-escaped block; a tight paragraph is written without the wrapper.</summary>
    /// <param name="source">Original UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="index">Index of the paragraph block.</param>
    /// <param name="tight">True to omit the <c>&lt;p&gt;</c> wrapper.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitParagraph(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int index,
        bool tight,
        IBufferWriter<byte> writer)
    {
        var block = blocks[index];
        if (tight)
        {
            InlineRenderer.Render(source.Slice(block.Start, block.Length), writer);
            EmitTightBreak(blocks, index + 1, writer);
            return;
        }

        Write("<p>"u8, writer);
        InlineRenderer.Render(source.Slice(block.Start, block.Length), writer);
        Write(ParagraphClose, writer);
    }

    /// <summary>Emits one paragraph spanning every consecutive <see cref="BlockKind.Paragraph"/> block from <paramref name="openerIndex"/>.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Full block descriptor span.</param>
    /// <param name="openerIndex">Index of the first paragraph block.</param>
    /// <param name="tight">True to omit the <c>&lt;p&gt;</c> wrapper.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the first non-Paragraph block (i.e. the next block to dispatch).</returns>
    private static int EmitParagraphRun(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int openerIndex,
        bool tight,
        IBufferWriter<byte> writer)
    {
        var end = openerIndex;
        while (end < blocks.Length && blocks[end].Kind is BlockKind.Paragraph)
        {
            end++;
        }

        if (!tight)
        {
            Write("<p>"u8, writer);
        }

        for (var i = openerIndex; i < end; i++)
        {
            if (i > openerIndex)
            {
                Write("\n"u8, writer);
            }

            var block = blocks[i];
            var line = source.Slice(block.Start, block.Length).TrimStart((byte)' ');
            InlineRenderer.Render(line, writer);
        }

        if (tight)
        {
            EmitTightBreak(blocks, end, writer);
            return end;
        }

        Write(ParagraphClose, writer);
        return end;
    }

    /// <summary>Dispatches block kinds deferred from the outer <see cref="Emit"/> switch.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="i">Current block index.</param>
    /// <param name="tight">True to emit paragraph text without <c>&lt;p&gt;</c> wrappers.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the last block this call consumed.</returns>
    private static int EmitDispatch(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int i,
        bool tight,
        IBufferWriter<byte> writer)
    {
        var block = blocks[i];
        switch (block.Kind)
        {
            case BlockKind.HtmlBlock or BlockKind.HtmlBlockContent:
                {
                    EmitHtmlBlockLine(source, block, writer);
                    return i;
                }

            case BlockKind.ThematicBreak:
                {
                    Write("<hr />\n"u8, writer);
                    return i;
                }

            case BlockKind.ListItem:
                {
                    return EmitList(source, blocks, i, writer);
                }

            case BlockKind.IndentedCode:
                {
                    return EmitIndentedCode(source, blocks, i, writer);
                }

            case BlockKind.Blank or BlockKind.None:
                {
                    return i;
                }

            default:
                {
                    EmitParagraph(source, blocks, i, tight, writer);
                    return i;
                }
        }
    }

    /// <summary>
    /// Writes a <c>&lt;pre&gt;&lt;code&gt;</c> block consuming a run of <see cref="BlockKind.IndentedCode"/> lines.
    /// Interleaved <see cref="BlockKind.Blank"/> lines are preserved as empty content lines, matching CommonMark §4.4.
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="openerIndex">Index of the first <see cref="BlockKind.IndentedCode"/> block.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the last block consumed by this run.</returns>
    /// <remarks>Internal blank lines do not terminate the block — they're part of the code body. The run ends at the first non-blank, non-indented-code line.</remarks>
    private static int EmitIndentedCode(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int openerIndex,
        IBufferWriter<byte> writer)
    {
        Write("<pre><code>"u8, writer);

        var lastConsumed = openerIndex;
        var pendingBlanks = 0;
        for (var j = openerIndex; j < blocks.Length; j++)
        {
            switch (blocks[j].Kind)
            {
                case BlockKind.IndentedCode:
                    {
                        while (pendingBlanks > 0)
                        {
                            Write("\n"u8, writer);
                            pendingBlanks--;
                        }

                        EmitIndentedCodeLine(source, blocks[j], writer);
                        lastConsumed = j;
                        continue;
                    }

                case BlockKind.Blank:
                    {
                        pendingBlanks++;
                        continue;
                    }

                case BlockKind.None:
                case BlockKind.AtxHeading:
                case BlockKind.SetextHeading:
                case BlockKind.ThematicBreak:
                case BlockKind.FencedCode:
                case BlockKind.FencedCodeContent:
                case BlockKind.BlockQuote:
                case BlockKind.ListItem:
                case BlockKind.ListItemContent:
                case BlockKind.Paragraph:
                case BlockKind.HtmlBlock:
                case BlockKind.HtmlBlockContent:
                    break;
            }

            // Any other block ends the run; trailing blanks are ignored (CommonMark trims trailing blank lines).
            break;
        }

        Write("</code></pre>\n"u8, writer);
        return lastConsumed;
    }

    /// <summary>Writes one indented-code body line, stripping the leading four-space indent and HTML-escaping the rest.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="block">Indented-code line block.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitIndentedCodeLine(ReadOnlySpan<byte> source, in BlockSpan block, IBufferWriter<byte> writer)
    {
        var line = source.Slice(block.Start, block.Length);
        var stripped = StripIndentPrefix(line);
        HtmlEscape.EscapeText(stripped, writer);
        Write("\n"u8, writer);
    }

    /// <summary>Strips the leading four-space indent (or one tab) from <paramref name="line"/>; falls back to the original slice when neither prefix is present.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>Slice without the indent prefix.</returns>
    private static ReadOnlySpan<byte> StripIndentPrefix(ReadOnlySpan<byte> line)
    {
        const int IndentColumn = 4;
        if (!line.IsEmpty && line[0] is (byte)'\t')
        {
            return line[1..];
        }

        var consumed = 0;
        while (consumed < IndentColumn && consumed < line.Length && line[consumed] is (byte)' ')
        {
            consumed++;
        }

        return line[consumed..];
    }

    /// <summary>Emits a run of same-kind <see cref="BlockKind.ListItem"/> blocks and their bodies as a single <c>&lt;ul&gt;</c> or <c>&lt;ol&gt;</c>.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Index of the first <see cref="BlockKind.ListItem"/>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the last block consumed; the outer loop's post-increment lands on the next sibling.</returns>
    /// <remarks>
    /// A list is loose when a blank line separates two items or two direct children of an item; a loose
    /// list wraps each item paragraph in <c>&lt;p&gt;</c>. Every item body is rendered as a nested document,
    /// so fenced code, headings, HTML blocks and nested lists inside an item render as they do at the top level.
    /// </remarks>
    private static int EmitList(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int start,
        IBufferWriter<byte> writer)
    {
        var firstLine = source.Slice(blocks[start].Start, blocks[start].Length);
        var ordered = IsOrderedItemLine(firstLine);
        var listEnd = FindListEnd(source, blocks, start, ordered);
        var loose = IsLooseList(source, blocks, start, listEnd);
        EmitListOpen(firstLine, ordered, writer);

        var i = start;
        while (i < listEnd)
        {
            var itemEnd = FindItemEnd(blocks, i + 1);
            EmitListItem(source, blocks, i, itemEnd, loose, writer);
            i = itemEnd;
        }

        Write(ordered ? "</ol>\n"u8 : "</ul>\n"u8, writer);
        return listEnd - 1;
    }

    /// <summary>Writes the opening <c>&lt;ul&gt;</c> or <c>&lt;ol&gt;</c> tag; an ordered list whose first number is not 1 carries a <c>start</c> attribute.</summary>
    /// <param name="firstLine">First item line of the list.</param>
    /// <param name="ordered">True when the list is ordered.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitListOpen(ReadOnlySpan<byte> firstLine, bool ordered, IBufferWriter<byte> writer)
    {
        if (!ordered)
        {
            Write("<ul>\n"u8, writer);
            return;
        }

        var startNumber = ReadOrderedStart(firstLine);
        if (startNumber is 1)
        {
            Write("<ol>\n"u8, writer);
            return;
        }

        Write("<ol start=\""u8, writer);
        var digits = writer.GetSpan(MaxFormattedIntLength);
        _ = startNumber.TryFormat(digits, out var written);
        writer.Advance(written);
        Write("\">\n"u8, writer);
    }

    /// <summary>True when the list-item line opens with a digit run (an ordered marker) rather than a bullet.</summary>
    /// <param name="line">UTF-8 list-item line.</param>
    /// <returns>True for an ordered item.</returns>
    private static bool IsOrderedItemLine(ReadOnlySpan<byte> line)
    {
        var i = SkipSpaces(line, 0);
        return i < line.Length && AsciiByteHelpers.IsAsciiDigit(line[i]);
    }

    /// <summary>Reads the number of an ordered list-item marker, saturating at <see cref="MaxOrderedDigits"/> digits.</summary>
    /// <param name="line">UTF-8 ordered-item line.</param>
    /// <returns>The marker's number.</returns>
    private static int ReadOrderedStart(ReadOnlySpan<byte> line)
    {
        var i = SkipSpaces(line, 0);
        var value = 0;
        for (var digits = 0; i < line.Length && digits < MaxOrderedDigits && AsciiByteHelpers.IsAsciiDigit(line[i]); digits++)
        {
            value = (value * DecimalBase) + (line[i] - (byte)'0');
            i++;
        }

        return value;
    }

    /// <summary>Advances past a digit run and its closing <c>.</c> or <c>)</c> at <paramref name="index"/>.</summary>
    /// <param name="line">UTF-8 line.</param>
    /// <param name="index">Offset of the first digit.</param>
    /// <returns>Offset just past the delimiter, or <paramref name="index"/> when the digits are not followed by one.</returns>
    private static int SkipOrderedMarker(ReadOnlySpan<byte> line, int index)
    {
        var i = index;
        while (i < line.Length && AsciiByteHelpers.IsAsciiDigit(line[i]))
        {
            i++;
        }

        return i < line.Length && line[i] is (byte)'.' or (byte)')' ? i + 1 : index;
    }

    /// <summary>Returns the index past the last block of the list that starts at <paramref name="start"/>.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Index of the list's first <see cref="BlockKind.ListItem"/>.</param>
    /// <param name="ordered">True when the list is ordered; an item of the other kind ends the list.</param>
    /// <returns>Index of the first block that is not part of the list.</returns>
    private static int FindListEnd(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int start,
        bool ordered)
    {
        var i = start;
        while (i < blocks.Length
               && blocks[i].Kind is BlockKind.ListItem
               && IsOrderedItemLine(source.Slice(blocks[i].Start, blocks[i].Length)) == ordered)
        {
            i = FindItemEnd(blocks, i + 1);
        }

        return i;
    }

    /// <summary>Returns the index past the last block belonging to the current <c>&lt;li&gt;</c>.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Index just after the <see cref="BlockKind.ListItem"/> opener.</param>
    /// <returns>Index of the next sibling list item, or first non-list block.</returns>
    private static int FindItemEnd(in ReadOnlySpan<BlockSpan> blocks, int start)
    {
        var i = start;
        while (i < blocks.Length && blocks[i].Kind is BlockKind.ListItemContent or BlockKind.Blank)
        {
            i++;
        }

        // Trailing blank lines that don't precede a continuation belong to the list (they may
        // sit before the next sibling) — but if they precede no further continuation and no
        // sibling list item, drop them from this item's range.
        if (i < blocks.Length && blocks[i].Kind is BlockKind.ListItem)
        {
            return i;
        }

        // Walk back over trailing Blanks to drop them from the item.
        while (i > start && blocks[i - 1].Kind is BlockKind.Blank)
        {
            i--;
        }

        return i;
    }

    /// <summary>True when a blank line separates two items of the list, or two direct children of any item.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Index of the list's first item.</param>
    /// <param name="listEnd">Exclusive end of the list.</param>
    /// <returns>True when the list is loose.</returns>
    private static bool IsLooseList(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int start,
        int listEnd)
    {
        var i = start;
        while (i < listEnd)
        {
            var itemEnd = FindItemEnd(blocks, i + 1);
            var blankBeforeSibling = itemEnd < listEnd && blocks[itemEnd - 1].Kind is BlockKind.Blank;
            if (blankBeforeSibling || ItemHasBlankBetweenChildren(source, blocks, i, itemEnd))
            {
                return true;
            }

            i = itemEnd;
        }

        return false;
    }

    /// <summary>True when the item body holds two direct child blocks separated by a blank line.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="opener">Index of the item's <see cref="BlockKind.ListItem"/> block.</param>
    /// <param name="end">Exclusive end of the item.</param>
    /// <returns>True when the item is loose on its own.</returns>
    private static bool ItemHasBlankBetweenChildren(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int opener,
        int end)
    {
        if (!HasInnerBlank(blocks, opener + 1, end))
        {
            return false;
        }

        var body = BuildItemBody(source, blocks, opener, end);
        ArrayBufferWriter<BlockSpan> bodyBlocks = new(InitialNestedBlockCapacity);
        _ = BlockScanner.Scan(body, bodyBlocks);
        return HasBlankBetweenChildren(body, bodyBlocks.WrittenSpan);
    }

    /// <summary>True when a <see cref="BlockKind.Blank"/> in [<paramref name="start"/>, <paramref name="end"/>) is followed by a non-blank block in the same range.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <returns>True when the range has a blank between two content blocks.</returns>
    private static bool HasInnerBlank(in ReadOnlySpan<BlockSpan> blocks, int start, int end)
    {
        var last = end - 1;
        while (last >= start && blocks[last].Kind is BlockKind.Blank)
        {
            last--;
        }

        for (var i = start; i < last; i++)
        {
            if (blocks[i].Kind is BlockKind.Blank)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the top-level blocks of an item body include a blank line between two children.</summary>
    /// <param name="body">De-indented item body.</param>
    /// <param name="blocks">Blocks scanned from <paramref name="body"/>.</param>
    /// <returns>True when a blank separates two direct children.</returns>
    private static bool HasBlankBetweenChildren(ReadOnlySpan<byte> body, in ReadOnlySpan<BlockSpan> blocks)
    {
        var seenChild = false;
        var blankAfterChild = false;
        var i = 0;
        while (i < blocks.Length)
        {
            if (blocks[i].Kind is BlockKind.Blank)
            {
                blankAfterChild = seenChild;
                i++;
                continue;
            }

            if (blankAfterChild)
            {
                return true;
            }

            seenChild = true;
            i = FindChildEnd(body, blocks, i);
        }

        return false;
    }

    /// <summary>Returns the index past the top-level child block that starts at <paramref name="index"/>.</summary>
    /// <param name="body">De-indented item body.</param>
    /// <param name="blocks">Blocks scanned from <paramref name="body"/>.</param>
    /// <param name="index">Index of the child's first block.</param>
    /// <returns>Index of the first block after the child; always greater than <paramref name="index"/>.</returns>
    private static int FindChildEnd(ReadOnlySpan<byte> body, in ReadOnlySpan<BlockSpan> blocks, int index) =>
        blocks[index].Kind switch
        {
            BlockKind.ListItem => FindListEnd(body, blocks, index, IsOrderedItemLine(body.Slice(blocks[index].Start, blocks[index].Length))),
            BlockKind.FencedCode => FindFenceChildEnd(blocks, index),
            BlockKind.IndentedCode => FindIndentedCodeChildEnd(blocks, index),
            BlockKind.Paragraph => FindKindRunEnd(blocks, index, BlockKind.Paragraph),
            BlockKind.HtmlBlock => FindKindRunEnd(blocks, index + 1, BlockKind.HtmlBlockContent),
            _ => index + 1
        };

    /// <summary>Returns the index past a fenced code block, including its closing fence.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="opener">Index of the opening fence.</param>
    /// <returns>Index of the first block after the fence.</returns>
    private static int FindFenceChildEnd(in ReadOnlySpan<BlockSpan> blocks, int opener)
    {
        var i = FindKindRunEnd(blocks, opener + 1, BlockKind.FencedCodeContent);
        return i < blocks.Length && blocks[i].Kind is BlockKind.FencedCode ? i + 1 : i;
    }

    /// <summary>Returns the index past an indented code block; blank lines between code lines belong to the block.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="opener">Index of the first indented-code line.</param>
    /// <returns>Index of the first block after the code block.</returns>
    private static int FindIndentedCodeChildEnd(in ReadOnlySpan<BlockSpan> blocks, int opener)
    {
        var last = opener;
        for (var i = opener + 1; i < blocks.Length && blocks[i].Kind is BlockKind.IndentedCode or BlockKind.Blank; i++)
        {
            if (blocks[i].Kind is BlockKind.IndentedCode)
            {
                last = i;
            }
        }

        return last + 1;
    }

    /// <summary>Returns the index of the first block at or after <paramref name="start"/> whose kind is not <paramref name="kind"/>.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="kind">Kind of the run.</param>
    /// <returns>End of the run.</returns>
    private static int FindKindRunEnd(in ReadOnlySpan<BlockSpan> blocks, int start, BlockKind kind)
    {
        var i = start;
        while (i < blocks.Length && blocks[i].Kind == kind)
        {
            i++;
        }

        return i;
    }

    /// <summary>Emits one <c>&lt;li&gt;</c> for the item spanning [<paramref name="opener"/>, <paramref name="end"/>).</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="opener">Index of the <see cref="BlockKind.ListItem"/> block.</param>
    /// <param name="end">Exclusive end index — first block that does NOT belong to this item.</param>
    /// <param name="loose">True when the owning list is loose.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitListItem(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int opener,
        int end,
        bool loose,
        IBufferWriter<byte> writer)
    {
        Write("<li>"u8, writer);

        var openerContent = StripListMarker(source.Slice(blocks[opener].Start, blocks[opener].Length));
        if (end == opener + 1 && !MayStartBlock(openerContent))
        {
            EmitSingleLineItem(openerContent, loose, writer);
        }
        else
        {
            EmitItemBody(BuildItemBody(source, blocks, opener, end), loose, writer);
        }

        Write("</li>\n"u8, writer);
    }

    /// <summary>Writes the text of a one-line item that cannot open another block; a loose item wraps it in a paragraph.</summary>
    /// <param name="content">Item text after the list marker.</param>
    /// <param name="loose">True when the owning list is loose.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitSingleLineItem(ReadOnlySpan<byte> content, bool loose, IBufferWriter<byte> writer)
    {
        if (!loose)
        {
            InlineRenderer.Render(content, writer);
            return;
        }

        Write("\n<p>"u8, writer);
        InlineRenderer.Render(content, writer);
        Write(ParagraphClose, writer);
    }

    /// <summary>True when a single-line item body could open a block other than a paragraph.</summary>
    /// <param name="content">Item text after the list marker.</param>
    /// <returns>True when the text is empty or starts with a block-level marker byte.</returns>
    private static bool MayStartBlock(ReadOnlySpan<byte> content) =>
        content.IsEmpty || BlockMarkerBytes.Contains(content[0]) || AsciiByteHelpers.IsAsciiDigit(content[0]);

    /// <summary>Joins the item's opener text and continuation lines into one document with the item's content indent removed.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="opener">Index of the <see cref="BlockKind.ListItem"/> block.</param>
    /// <param name="end">Exclusive end of the item.</param>
    /// <returns>Newline-terminated item body.</returns>
    private static byte[] BuildItemBody(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int opener,
        int end)
    {
        var openerContent = StripListMarker(source.Slice(blocks[opener].Start, blocks[opener].Length));
        var contentIndent = ContentIndentFromContinuations(blocks, opener + 1, end);

        var length = openerContent.Length + 1;
        for (var i = opener + 1; i < end; i++)
        {
            length += StripContentIndent(source.Slice(blocks[i].Start, blocks[i].Length), contentIndent).Length + 1;
        }

        var body = new byte[length];
        openerContent.CopyTo(body);
        var offset = openerContent.Length;
        body[offset] = (byte)'\n';
        offset++;

        for (var i = opener + 1; i < end; i++)
        {
            var line = StripContentIndent(source.Slice(blocks[i].Start, blocks[i].Length), contentIndent);
            line.CopyTo(body.AsSpan(offset));
            offset += line.Length;
            body[offset] = (byte)'\n';
            offset++;
        }

        return body;
    }

    /// <summary>Renders an item body as a nested document; a tight item leaves its paragraphs unwrapped.</summary>
    /// <param name="body">De-indented item body.</param>
    /// <param name="loose">True when the owning list is loose.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitItemBody(byte[] body, bool loose, IBufferWriter<byte> writer)
    {
        ArrayBufferWriter<BlockSpan> bodyBlocks = new(InitialNestedBlockCapacity);
        _ = BlockScanner.Scan(body, bodyBlocks);
        var blocks = bodyBlocks.WrittenSpan;

        var first = 0;
        while (first < blocks.Length && blocks[first].Kind is BlockKind.Blank)
        {
            first++;
        }

        if (first == blocks.Length)
        {
            return;
        }

        if (loose || blocks[first].Kind is not BlockKind.Paragraph)
        {
            Write("\n"u8, writer);
        }

        EmitBlocks(body, blocks, !loose, writer);
    }

    /// <summary>Returns the content indent recorded on the first continuation block, or 0 when none.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">First continuation index.</param>
    /// <param name="end">Exclusive end.</param>
    /// <returns>Content indent in bytes.</returns>
    private static int ContentIndentFromContinuations(in ReadOnlySpan<BlockSpan> blocks, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (blocks[i].Kind is BlockKind.ListItemContent)
            {
                return blocks[i].Level;
            }
        }

        return 0;
    }

    /// <summary>Drops up to <paramref name="contentIndent"/> leading space/tab bytes from <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 line.</param>
    /// <param name="contentIndent">Column to strip.</param>
    /// <returns>Line with leading list-content indent removed.</returns>
    private static ReadOnlySpan<byte> StripContentIndent(ReadOnlySpan<byte> line, int contentIndent)
    {
        var i = 0;
        while (i < line.Length && i < contentIndent && AsciiByteHelpers.IsAsciiHorizontalWhitespace(line[i]))
        {
            i++;
        }

        return line[i..];
    }

    /// <summary>Strips a bullet (<c>-</c>, <c>*</c>, <c>+</c>) or ordered (<c>1.</c>, <c>1)</c>) marker and the run of whitespace that follows it from the start of <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 list-item line.</param>
    /// <returns>The post-marker content span, with the trailing newline (if any) stripped.</returns>
    private static ReadOnlySpan<byte> StripListMarker(ReadOnlySpan<byte> line)
    {
        var i = SkipSpaces(line, 0);
        i = IsOrderedItemLine(line) ? SkipOrderedMarker(line, i) : SkipMarker(line, i);
        i = SkipSpaces(line, i);
        return AsciiByteHelpers.TrimTrailingNewline(line[i..]);
    }

    /// <summary>Advances <paramref name="index"/> past any run of ASCII spaces / tabs in <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 line.</param>
    /// <param name="index">Starting offset.</param>
    /// <returns>Offset of the first non-space byte at or after <paramref name="index"/>.</returns>
    private static int SkipSpaces(ReadOnlySpan<byte> line, int index)
    {
        while (index < line.Length && AsciiByteHelpers.IsAsciiHorizontalWhitespace(line[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>Advances past a single bullet marker byte (<c>-</c> / <c>*</c> / <c>+</c>) at <paramref name="index"/> when present.</summary>
    /// <param name="line">UTF-8 line.</param>
    /// <param name="index">Starting offset.</param>
    /// <returns>Offset just past the marker, or <paramref name="index"/> when no marker is present.</returns>
    private static int SkipMarker(ReadOnlySpan<byte> line, int index)
    {
        if (index >= line.Length)
        {
            return index;
        }

        var b = line[index];
        return b is (byte)'-' or (byte)'*' or (byte)'+' ? index + 1 : index;
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> to <paramref name="writer"/>.</summary>
    /// <param name="bytes">UTF-8 bytes to copy verbatim.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void Write(ReadOnlySpan<byte> bytes, IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }
}
