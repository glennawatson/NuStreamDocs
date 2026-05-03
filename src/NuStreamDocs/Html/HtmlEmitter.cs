// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Html;

/// <summary>
/// Renders <see cref="BlockSpan"/> sequences to UTF-8 HTML.
/// </summary>
/// <remarks>
/// Emits Material/Zensical-compatible markup: headings carry the
/// permalink anchor used by the embedded theme stylesheet, paragraphs
/// are wrapped in plain <c>&lt;p&gt;</c> elements. Inline parsing is
/// not yet implemented — content is currently HTML-escaped raw.
/// </remarks>
public static class HtmlEmitter
{
    /// <summary>Lowest CommonMark ATX heading level.</summary>
    private const int MinHeadingLevel = 1;

    /// <summary>Highest CommonMark ATX heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>Open-tag UTF-8 literals indexed by heading level.</summary>
    /// <remarks>
    /// Index 0 unused so <c>OpenTags[level]</c> is a direct lookup.
    /// Pre-baked UTF-8 keeps the emit path branch-free.
    /// </remarks>
    private static readonly byte[][] OpenTags =
    [
        [.. "<h?>"u8],
        [.. "<h1>"u8],
        [.. "<h2>"u8],
        [.. "<h3>"u8],
        [.. "<h4>"u8],
        [.. "<h5>"u8],
        [.. "<h6>"u8],
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
        [.. "</h6>\n"u8],
    ];

    /// <summary>
    /// Renders <paramref name="blocks"/> against <paramref name="source"/>
    /// into <paramref name="writer"/>.
    /// </summary>
    /// <param name="source">Original UTF-8 source the block descriptors index into.</param>
    /// <param name="blocks">Block descriptors emitted by <see cref="BlockScanner"/>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    [SuppressMessage(
        "Roslynator",
        "RCS1239:Use 'for' statement instead of 'while' statement",
        Justification = "Fenced-code emit consumes a variable number of sibling blocks per iteration; advancing 'i' inside the loop would trip S127 on a for-loop. While-loop keeps both rules happy.")]
    public static void Emit(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var i = 0;
        while (i < blocks.Length)
        {
            var block = blocks[i];
            switch (block.Kind)
            {
                case BlockKind.AtxHeading:
                {
                    EmitHeading(source, block, writer);
                    break;
                }

                case BlockKind.Paragraph:
                {
                    EmitParagraph(source, block, writer);
                    break;
                }

                case BlockKind.FencedCode:
                {
                    i = EmitFencedCode(source, blocks, i, writer);
                    break;
                }

                case BlockKind.FencedCodeContent:
                {
                    // Reached only when fences are unbalanced (no opener
                    // seen yet); treat as paragraph so the content still
                    // surfaces in the output.
                    EmitParagraph(source, block, writer);
                    break;
                }

                default:
                {
                    i = EmitDispatch(source, blocks, i, writer);
                    break;
                }
            }

            i++;
        }
    }

    /// <summary>Pulls the info-string (language tag) from a fenced-code opener line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="opener">Opener block.</param>
    /// <returns>Trimmed info string bytes; empty when none.</returns>
    /// <remarks>
    /// Exposed publicly so the parsing can be exercised directly from
    /// unit tests; callers outside the test surface still typically go
    /// through <see cref="Emit"/>.
    /// </remarks>
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

    /// <summary>Returns the trimmed body of the fence opener line, with the leading fence markers stripped.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="opener">Opener block.</param>
    /// <returns>Trimmed line content.</returns>
    private static ReadOnlySpan<byte> ExtractFenceInfoLine(ReadOnlySpan<byte> source, in BlockSpan opener)
    {
        var line = source.Slice(opener.Start, opener.Length);
        var marker = line.Length > 0 && line[0] == (byte)'~' ? (byte)'~' : (byte)'`';
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
        if (info.Length > 0)
        {
            Write(" class=\"language-"u8, writer);
            Write(info, writer);
            Write("\""u8, writer);
        }

        if (infoTail.Length > 0)
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

    /// <summary>Writes a paragraph-wrapped, HTML-escaped block.</summary>
    /// <param name="source">Original UTF-8 source buffer.</param>
    /// <param name="block">Paragraph block descriptor.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitParagraph(ReadOnlySpan<byte> source, in BlockSpan block, IBufferWriter<byte> writer)
    {
        Write("<p>"u8, writer);
        InlineRenderer.Render(source.Slice(block.Start, block.Length), writer);
        Write("</p>\n"u8, writer);
    }

    /// <summary>Dispatches block kinds the outer <see cref="Emit"/> switch defers to so its complexity stays under the analyzer ceiling.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="i">Current block index.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the last block this call consumed.</returns>
    private static int EmitDispatch(ReadOnlySpan<byte> source, in ReadOnlySpan<BlockSpan> blocks, int i, IBufferWriter<byte> writer)
    {
        var block = blocks[i];
        switch (block.Kind)
        {
            case BlockKind.HtmlBlock:
            case BlockKind.HtmlBlockContent:
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

            case BlockKind.Blank:
            case BlockKind.None:
            {
                return i;
            }

            default:
            {
                EmitParagraph(source, block, writer);
                return i;
            }
        }
    }

    /// <summary>Emits a contiguous run of <see cref="BlockKind.ListItem"/> + <see cref="BlockKind.ListItemContent"/> + <see cref="BlockKind.Blank"/> blocks as a single <c>&lt;ul&gt;</c>.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Index of the first <see cref="BlockKind.ListItem"/>.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Index of the last block consumed; the outer loop's post-increment lands on the next sibling.</returns>
    /// <remarks>
    /// Each item's body is grouped by blank-line separators; an item containing any blank-separated
    /// group becomes loose (every group wrapped in <c>&lt;p&gt;</c>). A body line whose stripped
    /// content is a thematic-break shape (<c>---</c> / <c>***</c> / <c>___</c>) emits an
    /// <c>&lt;hr/&gt;</c>. Nested lists are not yet handled.
    /// </remarks>
    private static int EmitList(ReadOnlySpan<byte> source, in ReadOnlySpan<BlockSpan> blocks, int start, IBufferWriter<byte> writer)
    {
        Write("<ul>\n"u8, writer);
        var i = start;
        while (i < blocks.Length && blocks[i].Kind is BlockKind.ListItem)
        {
            var itemEnd = FindItemEnd(blocks, i + 1);
            EmitListItem(source, blocks, i, itemEnd, writer);
            i = itemEnd;
        }

        Write("</ul>\n"u8, writer);
        return i - 1;
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

    /// <summary>Emits one <c>&lt;li&gt;</c> wrapping the bullet line content plus any continuation/blank blocks up to <paramref name="end"/>.</summary>
    /// <param name="source">UTF-8 source buffer.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="opener">Index of the <see cref="BlockKind.ListItem"/> block.</param>
    /// <param name="end">Exclusive end index — first block that does NOT belong to this item.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitListItem(ReadOnlySpan<byte> source, in ReadOnlySpan<BlockSpan> blocks, int opener, int end, IBufferWriter<byte> writer)
    {
        var openerLine = source.Slice(blocks[opener].Start, blocks[opener].Length);
        var openerInline = TrimTrailingNewline(StripBulletMarker(openerLine));
        var contentIndent = ContentIndentFromContinuations(blocks, opener + 1, end);
        var loose = HasBlankSeparator(blocks, opener + 1, end);

        Write("<li>"u8, writer);

        if (loose)
        {
            EmitLooseGroup(source, blocks, opener, end, openerInline, contentIndent, writer);
        }
        else
        {
            // Tight item: emit the opener inline directly, then any continuation lines
            // separated by `<br/>`-equivalent line breaks (here just newlines for inline render).
            InlineRenderer.Render(openerInline, writer);
            EmitTightContinuations(source, blocks, opener + 1, end, contentIndent, writer);
        }

        Write("</li>\n"u8, writer);
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

    /// <summary>True when <paramref name="blocks"/>[<paramref name="start"/>..<paramref name="end"/>] contains a Blank with non-Blank on both sides — a paragraph separator.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <returns>True when the item is loose.</returns>
    private static bool HasBlankSeparator(in ReadOnlySpan<BlockSpan> blocks, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (blocks[i].Kind is BlockKind.Blank && i + 1 < end && blocks[i + 1].Kind is BlockKind.ListItemContent)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Emits a loose-list <c>&lt;li&gt;</c> body — the opener inline becomes the first paragraph; each blank-separated group becomes a sibling paragraph or <c>&lt;hr/&gt;</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="opener">Opener index.</param>
    /// <param name="end">Exclusive end.</param>
    /// <param name="openerInline">Stripped opener content.</param>
    /// <param name="contentIndent">List content column.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitLooseGroup(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int opener,
        int end,
        ReadOnlySpan<byte> openerInline,
        int contentIndent,
        IBufferWriter<byte> writer)
    {
        Write("\n<p>"u8, writer);
        InlineRenderer.Render(openerInline, writer);
        EmitTightContinuations(source, blocks, opener + 1, FindFirstBlank(blocks, opener + 1, end), contentIndent, writer);
        Write("</p>\n"u8, writer);

        var i = FindFirstBlank(blocks, opener + 1, end);
        while (i < end)
        {
            // Skip the blank run.
            while (i < end && blocks[i].Kind is BlockKind.Blank)
            {
                i++;
            }

            if (i >= end)
            {
                break;
            }

            var groupEnd = FindFirstBlank(blocks, i, end);
            EmitItemBodyGroup(source, blocks, i, groupEnd, contentIndent, writer);
            i = groupEnd;
        }
    }

    /// <summary>Returns the index of the first <see cref="BlockKind.Blank"/> in [<paramref name="start"/>, <paramref name="end"/>), or <paramref name="end"/> when none.</summary>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <returns>First-blank index or <paramref name="end"/>.</returns>
    private static int FindFirstBlank(in ReadOnlySpan<BlockSpan> blocks, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (blocks[i].Kind is BlockKind.Blank)
            {
                return i;
            }
        }

        return end;
    }

    /// <summary>Emits one paragraph- or hr-shaped body group of a loose list item.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <param name="contentIndent">Column to strip.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitItemBodyGroup(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int start,
        int end,
        int contentIndent,
        IBufferWriter<byte> writer)
    {
        if (end - start is 1 && blocks[start].Kind is BlockKind.ListItemContent)
        {
            var stripped = StripContentIndent(source.Slice(blocks[start].Start, blocks[start].Length), contentIndent);
            if (IsThematicBreakLine(stripped))
            {
                Write("<hr />\n"u8, writer);
                return;
            }
        }

        Write("<p>"u8, writer);
        var first = true;
        for (var i = start; i < end; i++)
        {
            if (blocks[i].Kind is not BlockKind.ListItemContent)
            {
                continue;
            }

            var line = TrimTrailingNewline(StripContentIndent(source.Slice(blocks[i].Start, blocks[i].Length), contentIndent));
            if (!first)
            {
                Write("\n"u8, writer);
            }

            InlineRenderer.Render(line, writer);
            first = false;
        }

        Write("</p>\n"u8, writer);
    }

    /// <summary>Emits the continuation lines of a tight list item directly inside the <c>&lt;li&gt;</c>, separated by spaces.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="blocks">Block descriptors.</param>
    /// <param name="start">Inclusive start of continuations.</param>
    /// <param name="end">Exclusive end.</param>
    /// <param name="contentIndent">Column to strip from each continuation line.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitTightContinuations(
        ReadOnlySpan<byte> source,
        in ReadOnlySpan<BlockSpan> blocks,
        int start,
        int end,
        int contentIndent,
        IBufferWriter<byte> writer)
    {
        for (var i = start; i < end; i++)
        {
            if (blocks[i].Kind is not BlockKind.ListItemContent)
            {
                continue;
            }

            var line = TrimTrailingNewline(StripContentIndent(source.Slice(blocks[i].Start, blocks[i].Length), contentIndent));
            Write("\n"u8, writer);
            InlineRenderer.Render(line, writer);
        }
    }

    /// <summary>Drops up to <paramref name="contentIndent"/> leading space/tab bytes from <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 line.</param>
    /// <param name="contentIndent">Column to strip.</param>
    /// <returns>Line with leading list-content indent removed.</returns>
    private static ReadOnlySpan<byte> StripContentIndent(ReadOnlySpan<byte> line, int contentIndent)
    {
        var i = 0;
        while (i < line.Length && i < contentIndent && line[i] is (byte)' ' or (byte)'\t')
        {
            i++;
        }

        return line[i..];
    }

    /// <summary>True when <paramref name="line"/> is a thematic-break line (3+ runs of <c>-</c>, <c>*</c>, or <c>_</c>, optionally separated by spaces).</summary>
    /// <param name="line">Stripped line bytes.</param>
    /// <returns>True for a thematic break.</returns>
    private static bool IsThematicBreakLine(ReadOnlySpan<byte> line)
    {
        const int MinThematicRuns = 3;
        var trimmed = line.TrimEnd((byte)' ').TrimEnd((byte)'\t');
        if (trimmed.Length < MinThematicRuns)
        {
            return false;
        }

        var marker = trimmed[0];
        if (marker is not ((byte)'-' or (byte)'*' or (byte)'_'))
        {
            return false;
        }

        var count = 0;
        for (var i = 0; i < trimmed.Length; i++)
        {
            var b = trimmed[i];
            if (b == marker)
            {
                count++;
            }
            else if (b is not ((byte)' ' or (byte)'\t'))
            {
                return false;
            }
        }

        return count >= MinThematicRuns;
    }

    /// <summary>Strips a bullet marker (<c>-</c>, <c>*</c>, <c>+</c>) and the run of whitespace that follows it from the start of <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 list-item line.</param>
    /// <returns>The post-marker content span, with the trailing newline (if any) stripped.</returns>
    private static ReadOnlySpan<byte> StripBulletMarker(ReadOnlySpan<byte> line)
    {
        var i = SkipSpaces(line, 0);
        i = SkipMarker(line, i);
        i = SkipSpaces(line, i);
        return TrimTrailingNewline(line[i..]);
    }

    /// <summary>Advances <paramref name="index"/> past any run of ASCII spaces / tabs in <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 line.</param>
    /// <param name="index">Starting offset.</param>
    /// <returns>Offset of the first non-space byte at or after <paramref name="index"/>.</returns>
    private static int SkipSpaces(ReadOnlySpan<byte> line, int index)
    {
        while (index < line.Length && IsSpaceByte(line[index]))
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

    /// <summary>Drops a single trailing <c>\n</c> from <paramref name="span"/>.</summary>
    /// <param name="span">Input span.</param>
    /// <returns>Trimmed span.</returns>
    private static ReadOnlySpan<byte> TrimTrailingNewline(ReadOnlySpan<byte> span) =>
        span.Length > 0 && span[^1] is (byte)'\n' ? span[..^1] : span;

    /// <summary>True when <paramref name="b"/> is an ASCII space or tab.</summary>
    /// <param name="b">Byte under test.</param>
    /// <returns>True for <c>' '</c> or <c>'\t'</c>.</returns>
    private static bool IsSpaceByte(byte b) => b is (byte)' ' or (byte)'\t';

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
