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
/// materialization. Multi-line block grouping (paragraph absorption,
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
    /// <summary>Maximum heading level recognized by CommonMark ATX rules.</summary>
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

    /// <summary>ASCII case-fold bit; OR-ing with this on an ASCII letter yields its lowercase form.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Maximum length of any recognized HTML-block tag name (<c>blockquote</c> = 10 letters; rounded up).</summary>
    private const int MaxTagNameLength = 11;

    /// <summary>Lowercased UTF-8 byte arrays for every Type 6 tag name (CommonMark spec § 4.6 whitelist).</summary>
    private static readonly byte[][] Type6Tags =
    [
        [.. "address"u8],
        [.. "article"u8],
        [.. "aside"u8],
        [.. "base"u8],
        [.. "basefont"u8],
        [.. "blockquote"u8],
        [.. "body"u8],
        [.. "caption"u8],
        [.. "center"u8],
        [.. "col"u8],
        [.. "colgroup"u8],
        [.. "dd"u8],
        [.. "details"u8],
        [.. "dialog"u8],
        [.. "dir"u8],
        [.. "div"u8],
        [.. "dl"u8],
        [.. "dt"u8],
        [.. "fieldset"u8],
        [.. "figcaption"u8],
        [.. "figure"u8],
        [.. "footer"u8],
        [.. "form"u8],
        [.. "frame"u8],
        [.. "frameset"u8],
        [.. "h1"u8],
        [.. "h2"u8],
        [.. "h3"u8],
        [.. "h4"u8],
        [.. "h5"u8],
        [.. "h6"u8],
        [.. "head"u8],
        [.. "header"u8],
        [.. "hr"u8],
        [.. "html"u8],
        [.. "iframe"u8],
        [.. "legend"u8],
        [.. "li"u8],
        [.. "link"u8],
        [.. "main"u8],
        [.. "menu"u8],
        [.. "menuitem"u8],
        [.. "nav"u8],
        [.. "noframes"u8],
        [.. "ol"u8],
        [.. "optgroup"u8],
        [.. "option"u8],
        [.. "p"u8],
        [.. "param"u8],
        [.. "search"u8],
        [.. "section"u8],
        [.. "source"u8],
        [.. "summary"u8],
        [.. "table"u8],
        [.. "tbody"u8],
        [.. "td"u8],
        [.. "tfoot"u8],
        [.. "th"u8],
        [.. "thead"u8],
        [.. "title"u8],
        [.. "tr"u8],
        [.. "track"u8],
        [.. "ul"u8]
    ];

    /// <summary>Open HTML-block kind. <see cref="None"/> means no block is currently open.</summary>
    private enum HtmlBlockKind
    {
        /// <summary>No HTML block currently open.</summary>
        None = 0,

        /// <summary>Type 1 — <c>&lt;pre&gt;</c>; closes on <c>&lt;/pre&gt;</c> on any line.</summary>
        Pre,

        /// <summary>Type 1 — <c>&lt;script&gt;</c>; closes on <c>&lt;/script&gt;</c>.</summary>
        Script,

        /// <summary>Type 1 — <c>&lt;style&gt;</c>; closes on <c>&lt;/style&gt;</c>.</summary>
        Style,

        /// <summary>Type 1 — <c>&lt;textarea&gt;</c>; closes on <c>&lt;/textarea&gt;</c>.</summary>
        Textarea,

        /// <summary>Type 6 — recognized block-level tag; closes on the next blank line.</summary>
        Type6
    }

    /// <summary>
    /// Scans <paramref name="utf8"/> and writes one <see cref="BlockSpan"/> per
    /// recognized line to <paramref name="writer"/>.
    /// </summary>
    /// <param name="utf8">UTF-8 source buffer (no BOM expected).</param>
    /// <param name="writer">Sink for emitted block descriptors.</param>
    /// <returns>Number of blocks emitted.</returns>
    public static int Scan(ReadOnlySpan<byte> utf8, IBufferWriter<BlockSpan> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        FenceState fence = default;
        HtmlBlockState html = default;
        ListState list = default;
        var count = 0;
        var pos = 0;
        while (pos < utf8.Length)
        {
            var lineStart = pos;
            ReadLineExtents(utf8, pos, out var contentEnd, out var nextLine);

            var line = utf8[lineStart..contentEnd];
            var kind = ClassifyLine(line, ref fence, ref html, ref list, out var level);

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

    /// <summary>Classifies one already-trimmed-of-line-break line, with fence + html-block + list state.</summary>
    /// <param name="line">UTF-8 bytes of the line.</param>
    /// <param name="fence">Open fence state, mutated when this line opens or closes a fence.</param>
    /// <param name="html">Open html-block state, mutated when this line opens or closes a CommonMark HTML block.</param>
    /// <param name="list">Open list state, mutated when this line opens, continues, or closes a list.</param>
    /// <param name="level">Heading level / fence length / list indent populated on return.</param>
    /// <returns>Detected <see cref="BlockKind"/>.</returns>
    private static BlockKind ClassifyLine(ReadOnlySpan<byte> line, ref FenceState fence, ref HtmlBlockState html, ref ListState list, out int level)
    {
        level = 0;

        if (fence.IsOpen)
        {
            return ClassifyInsideFence(line, ref fence, out level);
        }

        if (html.IsOpen)
        {
            return ClassifyInsideHtmlBlock(line, ref html);
        }

        if (line.IsEmpty)
        {
            // Blank doesn't close the list; the next line decides.
            return BlockKind.Blank;
        }

        var indent = LeadingIndent(line);

        if (list.IsOpen && indent >= list.ContentIndent)
        {
            level = list.ContentIndent;
            return BlockKind.ListItemContent;
        }

        if (indent >= IndentedCodeColumn)
        {
            list = default;
            level = indent;
            return BlockKind.IndentedCode;
        }

        var kind = ClassifyContentLine(line[indent..], ref fence, ref html, out level);
        list = kind is BlockKind.ListItem
            ? new(ComputeListContentIndent(line, indent, level))
            : default;
        return kind;
    }

    /// <summary>Returns the content column for a list item — the column where the post-marker content actually begins.</summary>
    /// <param name="line">UTF-8 line.</param>
    /// <param name="indent">Leading indent (bytes; tabs counted as 1, matching <see cref="LeadingIndent"/>).</param>
    /// <param name="markerLength">Marker length (bullet = 1, ordered = digit count + delimiter byte).</param>
    /// <returns>Content column where continuation lines must reach to stay inside the item.</returns>
    /// <remarks>
    /// CommonMark says one mandatory space after the marker, plus up to three additional spaces are
    /// part of the marker indent. A single mandatory space gives the lower bound (<c>-foo</c>-style
    /// items don't reach this branch — the classifier rejects them). Past one mandatory space, we
    /// follow the actual whitespace run so author conventions like <c>-   text</c> (3 spaces, content
    /// at column 4) keep continuation lines aligned with their author intent.
    /// </remarks>
    private static int ComputeListContentIndent(ReadOnlySpan<byte> line, int indent, int markerLength)
    {
        var afterMarker = indent + markerLength;
        var p = afterMarker;
        while (p < line.Length && line[p] is Sp or Tab)
        {
            p++;
        }

        var spaceRun = p - afterMarker;
        return spaceRun is 0 ? afterMarker + 1 : afterMarker + spaceRun;
    }

    /// <summary>Classifies the indent-trimmed body of a content line.</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="fence">Open fence state to populate when this line opens a fence.</param>
    /// <param name="html">Open html-block state to populate when this line opens an HTML block.</param>
    /// <param name="level">Heading level / fence length / list indent populated on return.</param>
    /// <returns>Detected <see cref="BlockKind"/>.</returns>
    private static BlockKind ClassifyContentLine(ReadOnlySpan<byte> body, ref FenceState fence, ref HtmlBlockState html, out int level)
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

        if (TryClassifyHtmlBlockOpen(body, ref html))
        {
            return BlockKind.HtmlBlock;
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

        return TryClassifyListItem(body, out level) ? BlockKind.ListItem : BlockKind.Paragraph;
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

    /// <summary>Recognizes an ATX heading line.</summary>
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

    /// <summary>Recognizes an open fenced-code-block line and stamps the fence state.</summary>
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

    /// <summary>Recognizes a thematic-break line (<c>---</c>, <c>***</c>, <c>___</c>).</summary>
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

    /// <summary>Recognizes a setext underline (<c>===</c> or <c>---</c>).</summary>
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

    /// <summary>Recognizes a list-item marker.</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="level">List-marker length on success.</param>
    /// <returns>True when the line opens a list item.</returns>
    private static bool TryClassifyListItem(ReadOnlySpan<byte> body, out int level)
    {
        level = 0;
        var first = body[0];
        return first is Hyphen or Star or Plus
            ? TryClassifyBulletItem(body, out level)
            : first is >= (byte)'0' and <= (byte)'9' && TryClassifyOrderedItem(body, out level);
    }

    /// <summary>Recognizes a bullet list-item marker (<c>-</c>, <c>*</c>, <c>+</c>).</summary>
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

    /// <summary>Recognizes an ordered list-item marker (digits + <c>.</c>/<c>)</c>).</summary>
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

    /// <summary>Recognizes a CommonMark HTML-block opener at <paramref name="body"/>.</summary>
    /// <param name="body">Indent-trimmed line, leading byte already known to be <c>&lt;</c> when the open is real.</param>
    /// <param name="html">State to populate on success.</param>
    /// <returns>True when the line opens an HTML block.</returns>
    /// <remarks>
    /// CommonMark spec § 4.6 defines seven HTML-block kinds. This implementation covers Type 1
    /// (specific tags whose content is verbatim until a matching close tag) and Type 6 (a fixed
    /// list of block-level tag names whose block ends at the next blank line). Types 2–5 (HTML
    /// comment, processing instruction, declaration, CDATA) and Type 7 (any other complete tag
    /// shape) are not yet handled — they don't surface in the API-page output the renderer was
    /// failing on.
    /// </remarks>
    private static bool TryClassifyHtmlBlockOpen(ReadOnlySpan<byte> body, ref HtmlBlockState html)
    {
        Span<byte> tagBuffer = stackalloc byte[MaxTagNameLength];
        var tag = ExtractOpenerTagName(body, tagBuffer);
        if (tag.IsEmpty)
        {
            return false;
        }

        var type1 = MapType1Tag(tag);
        if (type1 is not HtmlBlockKind.None)
        {
            html = new(type1);
            return true;
        }

        if (!IsType6Tag(tag))
        {
            return false;
        }

        html = new(HtmlBlockKind.Type6);
        return true;
    }

    /// <summary>Extracts the lowercased tag name from a candidate HTML-block opener line into <paramref name="lowercaseBuffer"/>.</summary>
    /// <param name="body">Indent-trimmed line; must start with <c>&lt;</c> for a successful match.</param>
    /// <param name="lowercaseBuffer">Caller-owned scratch buffer (typically <c>stackalloc byte[<see cref="MaxTagNameLength"/>]</c>); the returned span is a slice of this buffer.</param>
    /// <returns>Lowercased tag-name slice (zero-length when the line isn't an HTML tag opener).</returns>
    private static ReadOnlySpan<byte> ExtractOpenerTagName(ReadOnlySpan<byte> body, Span<byte> lowercaseBuffer)
    {
        if (body.Length < 2 || body[0] != (byte)'<')
        {
            return default;
        }

        var tagStart = body[1] is (byte)'/' ? 2 : 1;
        return tagStart >= body.Length ? default : ExtractTagName(body, tagStart, lowercaseBuffer);
    }

    /// <summary>Maps a lowercased tag name to its <see cref="HtmlBlockKind"/> when it's one of the four CommonMark Type 1 tags; otherwise returns <see cref="HtmlBlockKind.None"/>.</summary>
    /// <param name="tag">Lowercased ASCII tag-name slice.</param>
    /// <returns>The matching kind or <see cref="HtmlBlockKind.None"/>.</returns>
    private static HtmlBlockKind MapType1Tag(ReadOnlySpan<byte> tag)
    {
        if (tag.SequenceEqual("pre"u8))
        {
            return HtmlBlockKind.Pre;
        }

        if (tag.SequenceEqual("script"u8))
        {
            return HtmlBlockKind.Script;
        }

        if (tag.SequenceEqual("style"u8))
        {
            return HtmlBlockKind.Style;
        }

        if (tag.SequenceEqual("textarea"u8))
        {
            return HtmlBlockKind.Textarea;
        }

        return HtmlBlockKind.None;
    }

    /// <summary>Returns the lower-cased ASCII tag name starting at <paramref name="offset"/>; empty when the bytes don't form a valid tag-name start.</summary>
    /// <param name="body">Indent-trimmed line.</param>
    /// <param name="offset">Offset just after <c>&lt;</c> (or <c>&lt;/</c>).</param>
    /// <param name="lowercaseBuffer">Caller-owned scratch buffer (typically <c>stackalloc</c>); the returned span slices into this buffer.</param>
    /// <returns>Lowercased ASCII slice of the tag name (zero-length when invalid).</returns>
    private static ReadOnlySpan<byte> ExtractTagName(ReadOnlySpan<byte> body, int offset, Span<byte> lowercaseBuffer)
    {
        if (offset >= body.Length || !IsAsciiLetter(body[offset]))
        {
            return default;
        }

        var end = offset;
        while (end < body.Length && IsTagNameContinue(body[end]))
        {
            end++;
        }

        var length = end - offset;
        return length > lowercaseBuffer.Length || !IsValidTagTerminator(body, end)
            ? default
            : CopyToLowercaseBuffer(body.Slice(offset, length), lowercaseBuffer);
    }

    /// <summary>Returns true when the byte at <paramref name="offset"/> in <paramref name="body"/> (or end-of-line) terminates a tag name validly (whitespace, <c>&gt;</c>, <c>/</c>).</summary>
    /// <param name="body">Source line.</param>
    /// <param name="offset">Position just past the tag-name characters.</param>
    /// <returns>True for a valid terminator.</returns>
    private static bool IsValidTagTerminator(ReadOnlySpan<byte> body, int offset)
    {
        var nextByte = offset < body.Length ? body[offset] : (byte)'>';
        return nextByte is (byte)' ' or (byte)'\t' or (byte)'>' or (byte)'/' or (byte)'\r' or (byte)'\n';
    }

    /// <summary>Copies <paramref name="source"/> into <paramref name="dst"/>, ASCII-folding A-Z into a-z.</summary>
    /// <param name="source">Source slice (caller has already bounded length to <paramref name="dst"/>'s length).</param>
    /// <param name="dst">Destination buffer (caller-owned; typically a <c>stackalloc</c>).</param>
    /// <returns>Span over the populated prefix of <paramref name="dst"/>.</returns>
    private static ReadOnlySpan<byte> CopyToLowercaseBuffer(ReadOnlySpan<byte> source, Span<byte> dst)
    {
        for (var i = 0; i < source.Length; i++)
        {
            var b = source[i];
            dst[i] = b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b | AsciiCaseBit) : b;
        }

        return dst[..source.Length];
    }

    /// <summary>Classifies a line while an HTML block is open — checks the close condition for the active <see cref="HtmlBlockKind"/> and clears <paramref name="html"/> on close.</summary>
    /// <param name="line">Raw line bytes (leading indent preserved — HTML blocks don't strip indent).</param>
    /// <param name="html">Open html-block state; reset to default when this line closes the block.</param>
    /// <returns>Always <see cref="BlockKind.HtmlBlockContent"/>; the closing line is part of the block (Type 1) or already excluded by the caller (Type 6).</returns>
    private static BlockKind ClassifyInsideHtmlBlock(ReadOnlySpan<byte> line, ref HtmlBlockState html) =>
        html.Kind is HtmlBlockKind.Type6
            ? ClassifyInsideType6Block(line, ref html)
            : ClassifyInsideType1Block(line, ref html);

    /// <summary>Classifies a line inside an open Type-6 HTML block; closes on a blank line.</summary>
    /// <param name="line">Line bytes.</param>
    /// <param name="html">Open state; reset to default when the block closes.</param>
    /// <returns><see cref="BlockKind.Blank"/> on close, otherwise <see cref="BlockKind.HtmlBlockContent"/>.</returns>
    private static BlockKind ClassifyInsideType6Block(ReadOnlySpan<byte> line, ref HtmlBlockState html)
    {
        if (line.IsEmpty || IsAllWhitespace(line))
        {
            html = default;
            return BlockKind.Blank;
        }

        return BlockKind.HtmlBlockContent;
    }

    /// <summary>Classifies a line inside an open Type-1 HTML block; closes on a case-insensitive match for the active tag's close form.</summary>
    /// <param name="line">Line bytes.</param>
    /// <param name="html">Open state; reset to default when the block closes (the closing line is still part of the block).</param>
    /// <returns>Always <see cref="BlockKind.HtmlBlockContent"/>.</returns>
    private static BlockKind ClassifyInsideType1Block(ReadOnlySpan<byte> line, ref HtmlBlockState html)
    {
        var closeNeedle = Type1CloseNeedle(html.Kind);
        if (closeNeedle.Length > 0 && IndexOfIgnoreCase(line, closeNeedle) >= 0)
        {
            html = default;
        }

        return BlockKind.HtmlBlockContent;
    }

    /// <summary>Maps a Type-1 <see cref="HtmlBlockKind"/> to its lowercased close-tag bytes.</summary>
    /// <param name="kind">Active block kind.</param>
    /// <returns>Lowercased close-tag bytes, or empty when <paramref name="kind"/> isn't a Type-1 kind.</returns>
    private static ReadOnlySpan<byte> Type1CloseNeedle(HtmlBlockKind kind) => kind switch
    {
        HtmlBlockKind.Pre => "</pre>"u8,
        HtmlBlockKind.Script => "</script>"u8,
        HtmlBlockKind.Style => "</style>"u8,
        HtmlBlockKind.Textarea => "</textarea>"u8,
        _ => default
    };

    /// <summary>Case-insensitive ASCII <see cref="MemoryExtensions.IndexOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T})"/> for the close-tag needles.</summary>
    /// <param name="haystack">Search target.</param>
    /// <param name="lowerNeedle">Lowercase ASCII needle.</param>
    /// <returns>Offset of first match or -1.</returns>
    private static int IndexOfIgnoreCase(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> lowerNeedle)
    {
        if (lowerNeedle.IsEmpty || haystack.Length < lowerNeedle.Length)
        {
            return -1;
        }

        for (var i = 0; i + lowerNeedle.Length <= haystack.Length; i++)
        {
            if (MatchesIgnoreCase(haystack[i..], lowerNeedle))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Returns true when the leading <paramref name="lowerNeedle"/>.Length bytes of <paramref name="haystack"/> equal <paramref name="lowerNeedle"/> ignoring ASCII case.</summary>
    /// <param name="haystack">Search target (must be at least <paramref name="lowerNeedle"/>.Length bytes).</param>
    /// <param name="lowerNeedle">Lowercase ASCII needle.</param>
    /// <returns>True on case-insensitive prefix match.</returns>
    private static bool MatchesIgnoreCase(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> lowerNeedle)
    {
        for (var j = 0; j < lowerNeedle.Length; j++)
        {
            var h = haystack[j];
            var lowered = h is >= (byte)'A' and <= (byte)'Z' ? (byte)(h | AsciiCaseBit) : h;
            if (lowered != lowerNeedle[j])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when every byte in <paramref name="line"/> is ASCII whitespace.</summary>
    /// <param name="line">Line bytes.</param>
    /// <returns>True when the line is whitespace-only.</returns>
    private static bool IsAllWhitespace(ReadOnlySpan<byte> line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] is not (Sp or Tab or Cr or Lf))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True when <paramref name="b"/> is an ASCII letter.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True when in [A-Za-z].</returns>
    private static bool IsAsciiLetter(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';

    /// <summary>True when <paramref name="b"/> can continue an HTML tag name.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True for letters, digits, or hyphens.</returns>
    private static bool IsTagNameContinue(byte b) =>
        IsAsciiLetter(b) || b is >= (byte)'0' and <= (byte)'9' or (byte)'-';

    /// <summary>True when <paramref name="tag"/> matches one of the CommonMark Type 6 block-level tags.</summary>
    /// <param name="tag">Lowercased ASCII tag-name slice.</param>
    /// <returns>True for any tag in the spec's Type 6 whitelist.</returns>
    /// <remarks>The full Type 6 list per CommonMark spec § 4.6.</remarks>
    private static bool IsType6Tag(ReadOnlySpan<byte> tag)
    {
        var tags = Type6Tags;
        for (var i = 0; i < tags.Length; i++)
        {
            if (tag.SequenceEqual(tags[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Open-fence state held across lines during a single scan.</summary>
    private readonly record struct FenceState(byte Marker, int Length)
    {
        /// <summary>Gets a value indicating whether a fence is currently open.</summary>
        public bool IsOpen => Length > 0;
    }

    /// <summary>Open-html-block state held across lines during a single scan.</summary>
    private readonly record struct HtmlBlockState(HtmlBlockKind Kind)
    {
        /// <summary>Gets a value indicating whether an HTML block is currently open.</summary>
        public bool IsOpen => Kind is not HtmlBlockKind.None;
    }

    /// <summary>Open-list state held across lines during a single scan.</summary>
    /// <param name="ContentIndent">Column at which continuation lines must start to stay inside the list item.</param>
    private readonly record struct ListState(int ContentIndent)
    {
        /// <summary>Gets a value indicating whether a list is currently open.</summary>
        public bool IsOpen => ContentIndent > 0;
    }
}
