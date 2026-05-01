// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Roslynator",
        "RCS1239:Use 'for' statement instead of 'while' statement",
        Justification = "Fenced-code emit consumes a variable number of sibling blocks per iteration; advancing 'i' inside the loop would trip S127 on a for-loop. While-loop keeps both rules happy.")]
    public static void Emit(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<BlockSpan> blocks,
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

                case BlockKind.Blank:
                case BlockKind.None:
                {
                    break;
                }

                default:
                {
                    EmitParagraph(source, block, writer);
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
        ReadOnlySpan<BlockSpan> blocks,
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
