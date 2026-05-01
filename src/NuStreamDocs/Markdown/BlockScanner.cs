// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Single-pass UTF-8 block scanner.
/// </summary>
/// <remarks>
/// Walks the source as <see cref="ReadOnlySpan{Byte}"/> and emits one
/// <see cref="BlockSpan"/> per line into a caller-supplied
/// <see cref="IBufferWriter{T}"/>. No per-line allocations; no string
/// materialisation. Multi-line block grouping (paragraph absorption,
/// list-item bodies, block-quote stitching) is a downstream concern —
/// the inline parser and emitter walk the line stream and merge as
/// needed.
/// <para>
/// Lines inside an open fenced code block are classified as
/// <see cref="BlockKind.FencedCodeContent"/> regardless of their
/// surface shape, so a stray <c>#</c> inside a fence does not look
/// like a heading.
/// </para>
/// </remarks>
public static class BlockScanner
{
    /// <summary>Maximum heading level recognised by CommonMark ATX rules.</summary>
    private const int MaxAtxLevel = 6;

    /// <summary>Loop bound for the leading-hash count.</summary>
    private const int AtxScanLimit = MaxAtxLevel + 1;

    /// <summary>Minimum number of identical run characters that form a thematic break or fence.</summary>
    private const int ThematicMinimum = 3;

    /// <summary>Indent-prefix size that triggers an indented code block.</summary>
    private const int IndentedCodeColumn = 4;

    /// <summary>Setext heading level emitted for the <c>=</c> underline.</summary>
    private const int SetextLevelEquals = 1;

    /// <summary>Setext heading level emitted for the <c>-</c> underline.</summary>
    private const int SetextLevelHyphen = 2;

    /// <summary>Line-feed byte.</summary>
    private const byte Lf = (byte)'\n';

    /// <summary>Carriage-return byte.</summary>
    private const byte Cr = (byte)'\r';

    /// <summary>Space byte.</summary>
    private const byte Sp = (byte)' ';

    /// <summary>Tab byte.</summary>
    private const byte Tab = (byte)'\t';

    /// <summary>Hash byte (ATX heading marker).</summary>
    private const byte Hash = (byte)'#';

    /// <summary>Backtick fence marker.</summary>
    private const byte Backtick = (byte)'`';

    /// <summary>Tilde fence marker.</summary>
    private const byte Tilde = (byte)'~';

    /// <summary>Asterisk thematic-break marker.</summary>
    private const byte Star = (byte)'*';

    /// <summary>Underscore thematic-break marker.</summary>
    private const byte Underscore = (byte)'_';

    /// <summary>Hyphen thematic-break / setext / list marker.</summary>
    private const byte Hyphen = (byte)'-';

    /// <summary>Plus list marker.</summary>
    private const byte Plus = (byte)'+';

    /// <summary>Equal-sign setext heading marker.</summary>
    private const byte EqualSign = (byte)'=';

    /// <summary>Greater-than block-quote marker.</summary>
    private const byte Gt = (byte)'>';

    /// <summary>
    /// Scans <paramref name="utf8"/> and writes one <see cref="BlockSpan"/> per
    /// recognised line to <paramref name="writer"/>.
    /// </summary>
    /// <param name="utf8">UTF-8 source buffer (no BOM expected).</param>
    /// <param name="writer">Sink for emitted block descriptors.</param>
    /// <returns>Number of blocks emitted.</returns>
    public static int Scan(ReadOnlySpan<byte> utf8, IBufferWriter<BlockSpan> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var fence = default(FenceState);
        var count = 0;
        var pos = 0;
        while (pos < utf8.Length)
        {
            var lineStart = pos;
            ReadLineExtents(utf8, pos, out var contentEnd, out var nextLine);

            var line = utf8[lineStart..contentEnd];
            var kind = ClassifyLine(line, ref fence, out var level);

            var span = writer.GetSpan(1);
            span[0] = new(kind, lineStart, contentEnd - lineStart, level);
            writer.Advance(1);
            count++;

            pos = nextLine;
        }

        return count;
    }

    /// <summary>Reads one line's content end and the start of the next line.</summary>
    /// <param name="utf8">Source buffer.</param>
    /// <param name="pos">Current position; start of the line.</param>
    /// <param name="contentEnd">Set to the byte after the line content (excludes CR/LF).</param>
    /// <param name="nextLine">Set to the byte after the line terminator.</param>
    private static void ReadLineExtents(ReadOnlySpan<byte> utf8, int pos, out int contentEnd, out int nextLine)
    {
        var lfOffset = utf8[pos..].IndexOf(Lf);
        if (lfOffset < 0)
        {
            contentEnd = utf8.Length;
            nextLine = utf8.Length;
            return;
        }

        var lfAbs = pos + lfOffset;
        nextLine = lfAbs + 1;
        contentEnd = lfOffset > 0 && utf8[lfAbs - 1] == Cr ? lfAbs - 1 : lfAbs;
    }

    /// <summary>Classifies one already-trimmed-of-line-break line, with fence state.</summary>
    /// <param name="line">UTF-8 bytes of the line.</param>
    /// <param name="fence">Open fence state, mutated when this line opens or closes a fence.</param>
    /// <param name="level">Heading level / fence length / list indent populated on return.</param>
    /// <returns>Detected <see cref="BlockKind"/>.</returns>
    private static BlockKind ClassifyLine(ReadOnlySpan<byte> line, ref FenceState fence, out int level)
    {
        level = 0;

        if (fence.IsOpen)
        {
            return ClassifyInsideFence(line, ref fence, out level);
        }

        if (line.IsEmpty)
        {
            return BlockKind.Blank;
        }

        var indent = LeadingIndent(line);
        if (indent >= IndentedCodeColumn)
        {
            level = indent;
            return BlockKind.IndentedCode;
        }

        return ClassifyContentLine(line[indent..], ref fence, out level);
    }

    /// <summary>Classifies the indent-trimmed body of a content line.</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="fence">Open fence state to populate when this line opens a fence.</param>
    /// <param name="level">Heading level / fence length / list indent populated on return.</param>
    /// <returns>Detected <see cref="BlockKind"/>.</returns>
    private static BlockKind ClassifyContentLine(ReadOnlySpan<byte> body, ref FenceState fence, out int level)
    {
        level = 0;
        if (body.IsEmpty)
        {
            return BlockKind.Paragraph;
        }

        if (TryClassifyFenceOpen(body, ref fence, out level))
        {
            return BlockKind.FencedCode;
        }

        if (TryClassifyAtxHeading(body, out level))
        {
            return BlockKind.AtxHeading;
        }

        if (TryClassifyThematicBreak(body))
        {
            return BlockKind.ThematicBreak;
        }

        if (TryClassifySetextUnderline(body, out level))
        {
            return BlockKind.SetextHeading;
        }

        if (body[0] == Gt)
        {
            return BlockKind.BlockQuote;
        }

        if (TryClassifyListItem(body, out level))
        {
            return BlockKind.ListItem;
        }

        return BlockKind.Paragraph;
    }

    /// <summary>Handles a line while a fenced code block is open.</summary>
    /// <param name="line">UTF-8 bytes.</param>
    /// <param name="fence">Open fence state; cleared when the closing fence is hit.</param>
    /// <param name="level">Fence-length echo on close, otherwise zero.</param>
    /// <returns><see cref="BlockKind.FencedCode"/> on the closing line; otherwise <see cref="BlockKind.FencedCodeContent"/>.</returns>
    private static BlockKind ClassifyInsideFence(ReadOnlySpan<byte> line, ref FenceState fence, out int level)
    {
        level = 0;
        var indent = LeadingIndent(line);
        if (indent >= IndentedCodeColumn)
        {
            return BlockKind.FencedCodeContent;
        }

        var body = line[indent..];
        if (body.IsEmpty || body[0] != fence.Marker)
        {
            return BlockKind.FencedCodeContent;
        }

        var run = MarkerRunLength(body, fence.Marker);
        if (run < fence.Length || HasTrailingNonWhitespace(body[run..]))
        {
            return BlockKind.FencedCodeContent;
        }

        level = run;
        fence = default;
        return BlockKind.FencedCode;
    }

    /// <summary>Counts the leading run of a single marker byte.</summary>
    /// <param name="body">Line body, indent-trimmed.</param>
    /// <param name="marker">Marker byte to count.</param>
    /// <returns>Run length.</returns>
    private static int MarkerRunLength(ReadOnlySpan<byte> body, byte marker)
    {
        var i = 0;
        while (i < body.Length && body[i] == marker)
        {
            i++;
        }

        return i;
    }

    /// <summary>True when <paramref name="tail"/> contains any non-whitespace byte.</summary>
    /// <param name="tail">Slice after the marker run.</param>
    /// <returns>True when at least one byte is not space or tab.</returns>
    private static bool HasTrailingNonWhitespace(ReadOnlySpan<byte> tail)
    {
        for (var i = 0; i < tail.Length; i++)
        {
            if (tail[i] is not Sp and not Tab)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts the leading-space-or-tab run.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>Indent column count.</returns>
    private static int LeadingIndent(ReadOnlySpan<byte> line)
    {
        var i = 0;
        while (i < line.Length && line[i] is Sp or Tab)
        {
            i++;
        }

        return i;
    }

    /// <summary>Recognises an ATX heading line.</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="level">Heading level on success.</param>
    /// <returns>True when <paramref name="body"/> is a valid ATX heading.</returns>
    private static bool TryClassifyAtxHeading(ReadOnlySpan<byte> body, out int level)
    {
        level = 0;
        if (body.IsEmpty || body[0] != Hash)
        {
            return false;
        }

        var hashes = 0;
        while (hashes < body.Length && hashes < AtxScanLimit && body[hashes] == Hash)
        {
            hashes++;
        }

        if (hashes is < 1 or > MaxAtxLevel)
        {
            return false;
        }

        if (hashes != body.Length && body[hashes] != Sp)
        {
            return false;
        }

        level = hashes;
        return true;
    }

    /// <summary>Recognises an open fenced-code-block line and stamps the fence state.</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="fence">Fence state to populate on success.</param>
    /// <param name="level">Fence run length on success.</param>
    /// <returns>True when <paramref name="body"/> opens a fence.</returns>
    private static bool TryClassifyFenceOpen(ReadOnlySpan<byte> body, ref FenceState fence, out int level)
    {
        level = 0;
        if (body.IsEmpty)
        {
            return false;
        }

        var marker = body[0];
        if (marker is not Backtick and not Tilde)
        {
            return false;
        }

        var run = MarkerRunLength(body, marker);
        if (run < ThematicMinimum)
        {
            return false;
        }

        // Backtick fences forbid backticks in their info string.
        if (marker == Backtick)
        {
            var info = body[run..];
            for (var i = 0; i < info.Length; i++)
            {
                if (info[i] == Backtick)
                {
                    return false;
                }
            }
        }

        fence = new(marker, run);
        level = run;
        return true;
    }

    /// <summary>Recognises a thematic-break line (<c>---</c>, <c>***</c>, <c>___</c>).</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <returns>True when the line is a thematic break.</returns>
    private static bool TryClassifyThematicBreak(ReadOnlySpan<byte> body)
    {
        var marker = body[0];
        if (marker is not Hyphen and not Star and not Underscore)
        {
            return false;
        }

        var run = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var b = body[i];
            if (b == marker)
            {
                run++;
                continue;
            }

            if (b is not Sp and not Tab)
            {
                return false;
            }
        }

        return run >= ThematicMinimum;
    }

    /// <summary>Recognises a setext underline (<c>===</c> or <c>---</c>).</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="level">1 for <c>=</c>, 2 for <c>-</c>.</param>
    /// <returns>True when <paramref name="body"/> is a setext underline.</returns>
    private static bool TryClassifySetextUnderline(ReadOnlySpan<byte> body, out int level)
    {
        level = 0;
        var marker = body[0];
        if (marker is not EqualSign and not Hyphen)
        {
            return false;
        }

        for (var i = 0; i < body.Length; i++)
        {
            var b = body[i];
            if (b == marker)
            {
                continue;
            }

            if (b is not Sp and not Tab)
            {
                return false;
            }
        }

        level = marker == EqualSign ? SetextLevelEquals : SetextLevelHyphen;
        return true;
    }

    /// <summary>Recognises a list-item marker.</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="level">List-marker length on success.</param>
    /// <returns>True when the line opens a list item.</returns>
    private static bool TryClassifyListItem(ReadOnlySpan<byte> body, out int level)
    {
        level = 0;
        var first = body[0];
        if (first is Hyphen or Star or Plus)
        {
            return TryClassifyBulletItem(body, out level);
        }

        if (first is not (>= (byte)'0' and <= (byte)'9'))
        {
            return false;
        }

        return TryClassifyOrderedItem(body, out level);
    }

    /// <summary>Recognises a bullet list-item marker (<c>-</c>, <c>*</c>, <c>+</c>).</summary>
    /// <param name="body">Indent-trimmed line, first byte already a bullet marker.</param>
    /// <param name="level">Marker length (always 1) on success.</param>
    /// <returns>True when the line is a bullet item.</returns>
    private static bool TryClassifyBulletItem(ReadOnlySpan<byte> body, out int level)
    {
        level = 0;
        if (body.Length != 1 && body[1] is not Sp and not Tab)
        {
            return false;
        }

        level = 1;
        return true;
    }

    /// <summary>Recognises an ordered list-item marker (digits + <c>.</c>/<c>)</c>).</summary>
    /// <param name="body">Indent-trimmed line, first byte already a digit.</param>
    /// <param name="level">Marker length (digit count + delimiter) on success.</param>
    /// <returns>True when the line is an ordered list item.</returns>
    private static bool TryClassifyOrderedItem(ReadOnlySpan<byte> body, out int level)
    {
        level = 0;
        var i = 0;
        while (i < body.Length && body[i] is >= (byte)'0' and <= (byte)'9')
        {
            i++;
        }

        if (i == body.Length || body[i] is not ((byte)'.' or (byte)')'))
        {
            return false;
        }

        var afterMark = i + 1;
        if (afterMark != body.Length && body[afterMark] is not Sp and not Tab)
        {
            return false;
        }

        level = afterMark;
        return true;
    }

    /// <summary>Open-fence state held across lines during a single scan.</summary>
    private readonly record struct FenceState(byte Marker, int Length)
    {
        /// <summary>Gets a value indicating whether a fence is currently open.</summary>
        public bool IsOpen => Length > 0;
    }
}
