// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// Pre-render pass that resolves CommonMark reference-style links. Definition lines
/// (<c>[label]: url</c>) are stripped from the output and every <c>[text][label]</c> /
/// <c>[text][]</c> / collapsed <c>[label]</c> reference is rewritten to inline
/// <c>[text](url)</c> form.
/// </summary>
public static class LinkReferenceRewriter
{
    /// <summary>Bytes consumed when skipping a backslash-escaped pair (<c>\\X</c>).</summary>
    private const int BackslashEscapeLength = 2;

    /// <summary>Length of the <c>]:</c> separator between a definition's label and its href.</summary>
    private const int LabelTerminatorLength = 2;

    /// <summary>Maximum indentation before a reference definition.</summary>
    private const int MaxDefinitionIndent = 3;

    /// <summary>Length of a title's opening and closing delimiters.</summary>
    private const int MinTitleLength = 2;

    /// <summary>Cap above which a parked definition table is dropped instead of cached, so an outlier page doesn't pin a large table.</summary>
    private const int MaxCachedTableCapacity = 4 * 1024;

    /// <summary>Per-thread parked definition table reused across rewrites on the same worker.</summary>
    [ThreadStatic]
    private static LinkReferenceTable? _tableCache;

    /// <summary>Skips the rewrite when the input has no chance of containing a definition line.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <returns>True when at least one <c>]:</c> sequence is present.</returns>
    public static bool MayContainReferences(ReadOnlySpan<byte> source) =>
        source.IndexOf("]:"u8) >= 0;

    /// <summary>Rewrites <paramref name="source"/> with all reference-style links inlined and definition lines removed.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <returns>The rewritten bytes; a copy of <paramref name="source"/> when no rewriting was needed.</returns>
    public static byte[] Rewrite(ReadOnlySpan<byte> source)
    {
        if (!MayContainReferences(source))
        {
            return source.ToArray();
        }

        ArrayBufferWriter<byte> writer = new(source.Length);
        return TryRewrite(source, writer) ? writer.WrittenSpan.ToArray() : source.ToArray();
    }

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="writer">UTF-8 sink; receives the rewritten output.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        if (!MayContainReferences(source) || !TryRewrite(source, writer))
        {
            Write(writer, source);
        }
    }

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/> when it defines at least one reference.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="writer">UTF-8 sink; untouched when no definition exists.</param>
    /// <returns>True when definitions were found and the rewritten output was written.</returns>
    private static bool TryRewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var table = _tableCache ?? new();
        _tableCache = null;
        try
        {
            table.Reset(source.Count("]:"u8));
            CollectDefinitions(source, table);
            if (table.Count is 0)
            {
                return false;
            }

            RewriteCore(source, table, writer);
            return true;
        }
        finally
        {
            if (table.Capacity <= MaxCachedTableCapacity)
            {
                _tableCache = table;
            }
        }
    }

    /// <summary>Two-pass rewrite once the definition set is known to be non-empty.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="definitions">Pre-built definition table.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void RewriteCore(
        ReadOnlySpan<byte> source,
        LinkReferenceTable definitions,
        IBufferWriter<byte> writer)
    {
        var pos = 0;
        while (pos < source.Length)
        {
            pos = ProcessOne(source, pos, definitions, writer);
        }
    }

    /// <summary>Handles one cursor position: code region, definition line, reference-link span, or plain bytes.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Current cursor.</param>
    /// <param name="definitions">Pre-built definition map.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Updated cursor.</returns>
    private static int ProcessOne(
        ReadOnlySpan<byte> source,
        int pos,
        LinkReferenceTable definitions,
        IBufferWriter<byte> writer)
    {
        if (TryConsumeCodeRegion(source, pos, writer, out var afterCode))
        {
            return afterCode;
        }

        if (MarkdownCodeScanner.AtLineStart(source, pos) && TryParseDefinition(source, pos, out _, out var definitionEnd))
        {
            return definitionEnd;
        }

        return source[pos] is (byte)'[' && TryRewriteReference(source, pos, definitions, writer, out var consumed)
            ? pos + consumed
            : CopyPlainRun(source, pos, writer);
    }

    /// <summary>Skips a fenced or inline-code region, copying it through verbatim.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Current cursor.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="afterCode">Cursor just past the consumed region on success.</param>
    /// <returns>True when a code region was consumed.</returns>
    private static bool TryConsumeCodeRegion(
        ReadOnlySpan<byte> source,
        int pos,
        IBufferWriter<byte> writer,
        out int afterCode)
    {
        afterCode = pos;
        if (MarkdownCodeScanner.AtLineStart(source, pos)
            && MarkdownCodeScanner.TryConsumeFence(source, pos, out var fenceEnd))
        {
            Write(writer, source[pos..fenceEnd]);
            afterCode = fenceEnd;
            return true;
        }

        if (source[pos] is not (byte)'`')
        {
            return false;
        }

        var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, pos);
        if (inlineEnd <= pos)
        {
            return false;
        }

        Write(writer, source[pos..inlineEnd]);
        afterCode = inlineEnd;
        return true;
    }

    /// <summary>Copies a plain (non-special) run forward, leveraging IndexOfAny to leap over prose.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Current cursor.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>Updated cursor.</returns>
    private static int CopyPlainRun(ReadOnlySpan<byte> source, int pos, IBufferWriter<byte> writer)
    {
        // The byte at pos didn't open a recognised construct; copy it through so we make forward
        // progress, then leap to the next special byte via IndexOfAny.
        var span = writer.GetSpan(1);
        span[0] = source[pos];
        writer.Advance(1);
        var start = pos + 1;
        if (start >= source.Length)
        {
            return start;
        }

        var rel = source[start..].IndexOfAny("`["u8);
        switch (rel)
        {
            case < 0:
                {
                    Write(writer, source[start..]);
                    return source.Length;
                }

            case > 0:
                {
                    Write(writer, source[start..(start + rel)]);
                    break;
                }
        }

        return start + rel;
    }

    /// <summary>Tries to rewrite a reference-style link at <paramref name="pos"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="pos">Cursor at the leading <c>[</c>.</param>
    /// <param name="definitions">Defined references.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="consumed">Bytes consumed from <paramref name="pos"/> on success.</param>
    /// <returns>True when a reference was rewritten.</returns>
    private static bool TryRewriteReference(
        ReadOnlySpan<byte> source,
        int pos,
        LinkReferenceTable definitions,
        IBufferWriter<byte> writer,
        out int consumed)
    {
        consumed = 0;
        var firstClose = FindMatchingBracket(source, pos);
        if (firstClose < 0)
        {
            return false;
        }

        // Inline link `[text](url)` — leave for the downstream parser; emit the literal so
        // LinkSpan can still rewrite it.
        var afterFirst = firstClose + 1;
        if (afterFirst < source.Length && source[afterFirst] is (byte)'(')
        {
            return false;
        }

        var label = source[(pos + 1)..firstClose];

        if (afterFirst < source.Length && source[afterFirst] is (byte)'[')
        {
            var secondClose = FindMatchingBracket(source, afterFirst);
            if (secondClose < 0)
            {
                return false;
            }

            var refLabel = source[(afterFirst + 1)..secondClose];
            if (refLabel.IsEmpty)
            {
                // Collapsed `[text][]` — text doubles as the label.
                refLabel = label;
            }

            if (!TryResolve(source, definitions, refLabel, out var href, out var title))
            {
                return false;
            }

            EmitInlineLink(label, href.AsSpan(source), title.AsSpan(source), writer);
            consumed = secondClose + 1 - pos;
            return true;
        }

        // Shortcut reference: `[label]` only.
        if (!TryResolve(source, definitions, label, out var shortcutHref, out var shortcutTitle))
        {
            return false;
        }

        EmitInlineLink(label, shortcutHref.AsSpan(source), shortcutTitle.AsSpan(source), writer);
        consumed = firstClose + 1 - pos;
        return true;
    }

    /// <summary>Emits <c>[text](href "title")</c> into <paramref name="writer"/>.</summary>
    /// <param name="text">Visible label bytes.</param>
    /// <param name="href">Resolved href bytes.</param>
    /// <param name="title">Resolved title bytes; empty for no title.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitInlineLink(
        ReadOnlySpan<byte> text,
        ReadOnlySpan<byte> href,
        ReadOnlySpan<byte> title,
        IBufferWriter<byte> writer)
    {
        Write(writer, "["u8);
        Write(writer, text);
        Write(writer, "]("u8);
        Write(writer, href);
        if (title is [_, ..])
        {
            Write(writer, " \""u8);
            WriteTitle(writer, title);
            Write(writer, "\""u8);
        }

        Write(writer, ")"u8);
    }

    /// <summary>Writes title bytes with each double quote as an entity so the title cannot end the inline link's quoted title early.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="title">Title bytes without delimiters.</param>
    private static void WriteTitle(IBufferWriter<byte> writer, ReadOnlySpan<byte> title)
    {
        var remaining = title;
        while (!remaining.IsEmpty)
        {
            var quote = remaining.IndexOf((byte)'"');
            if (quote < 0)
            {
                Write(writer, remaining);
                return;
            }

            Write(writer, remaining[..quote]);
            Write(writer, "&quot;"u8);
            remaining = remaining[(quote + 1)..];
        }
    }

    /// <summary>Resolves a reference label against the definition map.</summary>
    /// <param name="source">UTF-8 source the definitions were read from.</param>
    /// <param name="definitions">Defined references.</param>
    /// <param name="label">Raw label bytes.</param>
    /// <param name="href">Href range of the definition on hit.</param>
    /// <param name="title">Title range of the definition on hit.</param>
    /// <returns>True when the label is defined.</returns>
    private static bool TryResolve(
        ReadOnlySpan<byte> source,
        LinkReferenceTable definitions,
        ReadOnlySpan<byte> label,
        out ByteRange href,
        out ByteRange title)
    {
        href = default;
        title = default;
        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(label);
        return !trimmed.IsEmpty && definitions.TryGet(source, trimmed, out href, out title);
    }

    /// <summary>Walks <paramref name="source"/> once, filling <paramref name="map"/> with each definition; the first definition of a label wins.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="map">Destination table.</param>
    private static void CollectDefinitions(ReadOnlySpan<byte> source, LinkReferenceTable map)
    {
        var pos = 0;
        while (pos < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, pos)
                && MarkdownCodeScanner.TryConsumeFence(source, pos, out var fenceEnd))
            {
                pos = fenceEnd;
                continue;
            }

            if (TryParseDefinition(source, pos, out var def, out var definitionEnd))
            {
                _ = map.TryAdd(source, def.Label, def.Href, def.Title);
                pos = definitionEnd;
                continue;
            }

            pos = Utf8LineSpan.LfLineEnd(source, pos);
        }
    }

    /// <summary>
    /// Parses a definition that starts at <paramref name="lineStart"/>:
    /// <c>[label]: url "optional title"</c>, where the URL may sit on the line after the label
    /// and the title on the line after the URL.
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="lineStart">Inclusive start of the first line.</param>
    /// <param name="parsed">Parsed definition on success.</param>
    /// <param name="end">Exclusive end of the last line the definition occupies on success.</param>
    /// <returns>True when the lines form a well-formed definition.</returns>
    private static bool TryParseDefinition(
        ReadOnlySpan<byte> source,
        int lineStart,
        out ParsedDefinition parsed,
        out int end)
    {
        parsed = default;
        end = lineStart;
        var lineEnd = Utf8LineSpan.LfLineEnd(source, lineStart);
        if (!TryParseDefinitionLabel(source, lineStart, lineEnd, out var label, out var afterColon))
        {
            return false;
        }

        if (!TryLocateDefinitionHref(source, afterColon, lineEnd, out var hrefStart, out var hrefLineEnd))
        {
            return false;
        }

        if (!TryParseDefinitionHref(source, hrefStart, hrefLineEnd, out var hrefBytes, out var afterHref))
        {
            return false;
        }

        var afterSpaces = SkipSpaces(source, afterHref, hrefLineEnd);
        if (afterSpaces < hrefLineEnd && !IsLineBreak(source[afterSpaces]))
        {
            if (afterSpaces == afterHref || !TryParseDefinitionTitle(source, afterSpaces, hrefLineEnd, out var sameLineTitle))
            {
                return false;
            }

            parsed = new(RangeOf(source, label), RangeOf(source, hrefBytes), RangeOf(source, sameLineTitle));
            end = hrefLineEnd;
            return true;
        }

        var hasNextLineTitle = TryParseNextLineTitle(source, hrefLineEnd, out var nextLineTitle, out var nextLineEnd);
        parsed = new(RangeOf(source, label), RangeOf(source, hrefBytes), RangeOf(source, nextLineTitle));
        end = hasNextLineTitle ? nextLineEnd : hrefLineEnd;
        return true;
    }

    /// <summary>Finds where a definition's URL starts: after the colon on the label line, or at the first content of the next line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="afterColon">Cursor just after the colon and any inline whitespace, within the label line.</param>
    /// <param name="lineEnd">Exclusive end of the label line.</param>
    /// <param name="hrefStart">Index of the URL's first byte on success.</param>
    /// <param name="hrefLineEnd">Exclusive end of the line holding the URL on success.</param>
    /// <returns>True when a URL start was found.</returns>
    private static bool TryLocateDefinitionHref(
        ReadOnlySpan<byte> source,
        int afterColon,
        int lineEnd,
        out int hrefStart,
        out int hrefLineEnd)
    {
        hrefStart = afterColon;
        hrefLineEnd = lineEnd;
        if (!IsLineBreak(source[afterColon]))
        {
            return true;
        }

        hrefLineEnd = Utf8LineSpan.LfLineEnd(source, lineEnd);
        hrefStart = SkipSpaces(source, lineEnd, hrefLineEnd);
        return hrefStart < hrefLineEnd && !IsLineBreak(source[hrefStart]);
    }

    /// <summary>Parses a title that occupies the whole line starting at <paramref name="lineStart"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="lineStart">Inclusive start of the candidate line.</param>
    /// <param name="title">Title bytes without delimiters on success.</param>
    /// <param name="lineEnd">Exclusive end of the title line on success.</param>
    /// <returns>True when the line is a well-formed title.</returns>
    private static bool TryParseNextLineTitle(
        ReadOnlySpan<byte> source,
        int lineStart,
        out ReadOnlySpan<byte> title,
        out int lineEnd)
    {
        title = default;
        lineEnd = Utf8LineSpan.LfLineEnd(source, lineStart);
        var start = SkipSpaces(source, lineStart, lineEnd);
        return start < lineEnd && TryParseDefinitionTitle(source, start, lineEnd, out title);
    }

    /// <summary>Parses the quoted or parenthesized title that ends a definition line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Cursor at the title's opening delimiter.</param>
    /// <param name="lineEnd">End of the line.</param>
    /// <param name="title">Title bytes without delimiters on success; empty for an empty title.</param>
    /// <returns>True when the delimiters are matched and nothing follows the closing delimiter.</returns>
    private static bool TryParseDefinitionTitle(
        ReadOnlySpan<byte> source,
        int start,
        int lineEnd,
        out ReadOnlySpan<byte> title)
    {
        title = default;
        var closer = source[start] switch
        {
            (byte)'"' => '"',
            (byte)'\'' => '\'',
            (byte)'(' => ')',
            _ => '\0'
        };

        var end = lineEnd;
        while (end > start && AsciiByteHelpers.IsAsciiWhitespace(source[end - 1]))
        {
            end--;
        }

        if (closer == '\0' || end - start < MinTitleLength || source[end - 1] != closer)
        {
            return false;
        }

        title = source[(start + 1)..(end - 1)];
        return true;
    }

    /// <summary>Returns true for a line-terminator byte.</summary>
    /// <param name="b">Byte to test.</param>
    /// <returns>True for <c>\n</c> or <c>\r</c>.</returns>
    private static bool IsLineBreak(byte b) => b is (byte)'\n' or (byte)'\r';

    /// <summary>Parses the <c>[label]:</c> prefix of a definition line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="lineStart">Inclusive line start.</param>
    /// <param name="lineEnd">Exclusive line end.</param>
    /// <param name="label">Trimmed label bytes on success.</param>
    /// <param name="afterColon">Cursor position just after the colon (and any trailing inline whitespace) on success.</param>
    /// <returns>True when the prefix is well-formed and a non-empty label was found.</returns>
    private static bool TryParseDefinitionLabel(
        ReadOnlySpan<byte> source,
        int lineStart,
        int lineEnd,
        out ReadOnlySpan<byte> label,
        out int afterColon)
    {
        label = default;
        afterColon = 0;
        var p = SkipIndent(source, lineStart, lineEnd, MaxDefinitionIndent);
        if (p >= lineEnd || source[p] is not (byte)'[')
        {
            return false;
        }

        var labelClose = FindMatchingBracketOnLine(source, p, lineEnd);
        if (labelClose < 0 || labelClose + 1 >= lineEnd || source[labelClose + 1] is not (byte)':')
        {
            return false;
        }

        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(source[(p + 1)..labelClose]);
        if (trimmed.IsEmpty)
        {
            return false;
        }

        afterColon = SkipSpaces(source, labelClose + LabelTerminatorLength, lineEnd);
        if (afterColon >= lineEnd)
        {
            return false;
        }

        label = trimmed;
        return true;
    }

    /// <summary>Parses the URL portion of a definition line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Cursor at the first byte of the URL.</param>
    /// <param name="lineEnd">End of the line.</param>
    /// <param name="hrefBytes">Resolved href bytes (without surrounding angle brackets) on success.</param>
    /// <param name="afterHref">Cursor just past the URL (and its closing <c>&gt;</c> when present) on success.</param>
    /// <returns>True when a non-empty URL was parsed.</returns>
    private static bool TryParseDefinitionHref(
        ReadOnlySpan<byte> source,
        int start,
        int lineEnd,
        out ReadOnlySpan<byte> hrefBytes,
        out int afterHref)
    {
        var hrefEnd = ScanHref(source, start, lineEnd, out var hrefSawAngle);
        if (hrefEnd <= start)
        {
            hrefBytes = default;
            afterHref = start;
            return false;
        }

        hrefBytes = hrefSawAngle ? source[(start + 1)..(hrefEnd - 1)] : source[start..hrefEnd];
        afterHref = hrefEnd;
        return true;
    }

    /// <summary>Scans the URL portion of a definition.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">First byte of the URL.</param>
    /// <param name="lineEnd">End of the line.</param>
    /// <param name="sawAngleBrackets">True when wrapped in <c>&lt;…&gt;</c>.</param>
    /// <returns>Exclusive end of the URL.</returns>
    private static int ScanHref(ReadOnlySpan<byte> source, int start, int lineEnd, out bool sawAngleBrackets)
    {
        if (source[start] is (byte)'<')
        {
            return ScanAngleBracketedHref(source, start, lineEnd, out sawAngleBrackets);
        }

        sawAngleBrackets = false;
        return ScanBareHref(source, start, lineEnd);
    }

    /// <summary>Scans an angle-bracketed URL of the form <c>&lt;href&gt;</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Index of the opening <c>&lt;</c>.</param>
    /// <param name="lineEnd">End of the line.</param>
    /// <param name="sawAngleBrackets">True when the closing <c>&gt;</c> was matched.</param>
    /// <returns>Exclusive end of the URL (past the closing <c>&gt;</c>) on success; <paramref name="start"/> on failure.</returns>
    private static int ScanAngleBracketedHref(
        ReadOnlySpan<byte> source,
        int start,
        int lineEnd,
        out bool sawAngleBrackets)
    {
        for (var i = start + 1; i < lineEnd; i++)
        {
            var b = source[i];
            if (b is (byte)'>')
            {
                sawAngleBrackets = true;
                return i + 1;
            }

            if (b is (byte)'\n' or (byte)'\r' or (byte)'<')
            {
                break;
            }
        }

        sawAngleBrackets = false;
        return start;
    }

    /// <summary>Scans a bare URL up to the next ASCII whitespace byte.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">First byte of the URL.</param>
    /// <param name="lineEnd">End of the line.</param>
    /// <returns>Exclusive end of the URL.</returns>
    private static int ScanBareHref(ReadOnlySpan<byte> source, int start, int lineEnd)
    {
        var p = start;
        while (p < lineEnd && !AsciiByteHelpers.IsAsciiWhitespace(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Skips up to <paramref name="maxIndent"/> spaces at the start of a line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="lineStart">Line start.</param>
    /// <param name="lineEnd">Line end.</param>
    /// <param name="maxIndent">Maximum spaces to consume.</param>
    /// <returns>Updated index.</returns>
    private static int SkipIndent(ReadOnlySpan<byte> source, int lineStart, int lineEnd, int maxIndent)
    {
        var p = lineStart;
        for (var consumed = 0; p < lineEnd && consumed < maxIndent && source[p] is (byte)' '; consumed++)
        {
            p++;
        }

        return p;
    }

    /// <summary>Skips spaces / tabs forward.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="p">Start.</param>
    /// <param name="lineEnd">Line end.</param>
    /// <returns>Updated index.</returns>
    private static int SkipSpaces(ReadOnlySpan<byte> source, int p, int lineEnd)
    {
        while (p < lineEnd && AsciiByteHelpers.IsAsciiHorizontalWhitespace(source[p]))
        {
            p++;
        }

        return p;
    }

    /// <summary>Locates the matching close bracket; respects nested pairs and backslash escapes.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="openIndex">Index of the opening bracket.</param>
    /// <returns>Index of the close, or <c>-1</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindMatchingBracket(ReadOnlySpan<byte> source, int openIndex) =>
        FindMatchingBracketOnLine(source, openIndex, source.Length);

    /// <summary>Locates the matching close bracket bounded by <paramref name="lineEnd"/>; respects nested pairs and backslash escapes.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="openIndex">Index of the opening bracket.</param>
    /// <param name="lineEnd">End of the search range.</param>
    /// <returns>Index of the close, or <c>-1</c>.</returns>
    private static int FindMatchingBracketOnLine(ReadOnlySpan<byte> source, int openIndex, int lineEnd)
    {
        var depth = 1;
        var i = openIndex + 1;
        while (i < lineEnd)
        {
            switch (source[i])
            {
                case (byte)'\\' when i + 1 < lineEnd:
                    {
                        i += BackslashEscapeLength;
                        continue;
                    }

                case (byte)'[':
                    {
                        depth++;
                        break;
                    }

                case (byte)']':
                    {
                        depth--;
                        if (depth == 0)
                        {
                            return i;
                        }

                        break;
                    }
            }

            i++;
        }

        return -1;
    }

    /// <summary>Bulk-writes <paramref name="bytes"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Returns the range of <paramref name="slice"/> within <paramref name="source"/>.</summary>
    /// <param name="source">UTF-8 source the slice was cut from.</param>
    /// <param name="slice">Slice of <paramref name="source"/>; empty for none.</param>
    /// <returns>The slice's offset and length; the default range when the slice is empty.</returns>
    private static ByteRange RangeOf(ReadOnlySpan<byte> source, ReadOnlySpan<byte> slice) =>
        slice.IsEmpty
            ? default
            : new((int)Unsafe.ByteOffset(ref MemoryMarshal.GetReference(source), ref MemoryMarshal.GetReference(slice)), slice.Length);

    /// <summary>Transient parse result used during the collection pass.</summary>
    /// <param name="Label">Label range.</param>
    /// <param name="Href">Href range without surrounding angle brackets.</param>
    /// <param name="Title">Title range without delimiters; empty when the definition has no title.</param>
    private readonly record struct ParsedDefinition(ByteRange Label, ByteRange Href, ByteRange Title);
}
