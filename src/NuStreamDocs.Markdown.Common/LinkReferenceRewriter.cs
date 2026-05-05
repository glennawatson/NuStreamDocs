// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// Pre-render pass that resolves CommonMark reference-style links.
/// </summary>
/// <remarks>
/// Walks each non-fenced line, builds a label → href map from definition lines
/// (<c>[label]: url "optional title"</c>), then rewrites every <c>[text][label]</c> /
/// <c>[text][]</c> / collapsed <c>[label]</c> reference into the inline form
/// <c>[text](url "title")</c>. The definition lines themselves are stripped from the output. The
/// downstream <c>InlineRenderer</c> never has to know about references — the rewriter hands it
/// inline-form links it already understands.
/// </remarks>
public static class LinkReferenceRewriter
{
    /// <summary>Bytes consumed when skipping a backslash-escaped pair (<c>\\X</c>).</summary>
    private const int BackslashEscapeLength = 2;

    /// <summary>Length of the <c>]:</c> separator between a definition's label and its href.</summary>
    private const int LabelTerminatorLength = 2;

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

        var definitions = CollectDefinitions(source);
        if (definitions.Count is 0)
        {
            return source.ToArray();
        }

        ArrayBufferWriter<byte> writer = new(source.Length);
        RewriteCore(source, definitions, writer);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="writer">UTF-8 sink; receives the rewritten output.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (!MayContainReferences(source))
        {
            Write(writer, source);
            return;
        }

        var definitions = CollectDefinitions(source);
        if (definitions.Count is 0)
        {
            Write(writer, source);
            return;
        }

        RewriteCore(source, definitions, writer);
    }

    /// <summary>Two-pass rewrite once the definition set is known to be non-empty.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="definitions">Pre-built case-folded label → definition map.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void RewriteCore(ReadOnlySpan<byte> source, Dictionary<string, Definition> definitions, IBufferWriter<byte> writer)
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
    private static int ProcessOne(ReadOnlySpan<byte> source, int pos, Dictionary<string, Definition> definitions, IBufferWriter<byte> writer)
    {
        if (TryConsumeCodeRegion(source, pos, writer, out var afterCode))
        {
            return afterCode;
        }

        if (MarkdownCodeScanner.AtLineStart(source, pos))
        {
            var lineEnd = MarkdownCodeScanner.LineEnd(source, pos);
            if (TryParseDefinitionLine(source, pos, lineEnd, out _))
            {
                return lineEnd;
            }
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
    private static bool TryConsumeCodeRegion(ReadOnlySpan<byte> source, int pos, IBufferWriter<byte> writer, out int afterCode)
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
    private static bool TryRewriteReference(ReadOnlySpan<byte> source, int pos, Dictionary<string, Definition> definitions, IBufferWriter<byte> writer, out int consumed)
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

            if (!TryResolve(definitions, refLabel, out var def))
            {
                return false;
            }

            EmitInlineLink(label, in def, writer);
            consumed = secondClose + 1 - pos;
            return true;
        }

        // Shortcut reference: `[label]` only.
        if (!TryResolve(definitions, label, out var defShortcut))
        {
            return false;
        }

        EmitInlineLink(label, in defShortcut, writer);
        consumed = firstClose + 1 - pos;
        return true;
    }

    /// <summary>Emits <c>[text](href)</c> into <paramref name="writer"/>.</summary>
    /// <param name="text">Visible label bytes.</param>
    /// <param name="def">Resolved definition.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <remarks>
    /// CommonMark titles are dropped: the downstream <c>LinkSpan</c> parser treats the entire
    /// <c>(…)</c> body as the href, so emitting <c>(url "title")</c> would corrupt the rendered href.
    /// </remarks>
    private static void EmitInlineLink(ReadOnlySpan<byte> text, in Definition def, IBufferWriter<byte> writer)
    {
        Write(writer, "["u8);
        Write(writer, text);
        Write(writer, "]("u8);
        Write(writer, def.Href);
        Write(writer, ")"u8);
    }

    /// <summary>Resolves a reference label against the definition map.</summary>
    /// <param name="definitions">Defined references.</param>
    /// <param name="label">Raw label bytes.</param>
    /// <param name="def">Resolved definition on hit.</param>
    /// <returns>True when the label is in the map.</returns>
    private static bool TryResolve(Dictionary<string, Definition> definitions, ReadOnlySpan<byte> label, out Definition def)
    {
        def = default;
        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(label);
        if (trimmed.IsEmpty)
        {
            return false;
        }

        var key = NormalizeLabel(trimmed);
        return definitions.TryGetValue(key, out def!);
    }

    /// <summary>Walks <paramref name="source"/> once, building the definition map.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <returns>Map keyed on the case-folded label.</returns>
    private static Dictionary<string, Definition> CollectDefinitions(ReadOnlySpan<byte> source)
    {
        Dictionary<string, Definition> map = new(StringComparer.Ordinal);
        var pos = 0;
        while (pos < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, pos)
                && MarkdownCodeScanner.TryConsumeFence(source, pos, out var fenceEnd))
            {
                pos = fenceEnd;
                continue;
            }

            var lineEnd = MarkdownCodeScanner.LineEnd(source, pos);
            if (TryParseDefinitionLine(source, pos, lineEnd, out var def))
            {
                map.TryAdd(def.Key, new(def.Href.ToArray()));
            }

            pos = lineEnd;
        }

        return map;
    }

    /// <summary>Parses a single line as <c>[label]: url "optional title"</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="lineStart">Inclusive line start.</param>
    /// <param name="lineEnd">Exclusive line end.</param>
    /// <param name="parsed">Parsed definition on success.</param>
    /// <returns>True when the line is a well-formed definition.</returns>
    private static bool TryParseDefinitionLine(ReadOnlySpan<byte> source, int lineStart, int lineEnd, out ParsedDefinition parsed)
    {
        parsed = default;
        if (!TryParseDefinitionLabel(source, lineStart, lineEnd, out var label, out var afterColon))
        {
            return false;
        }

        if (!TryParseDefinitionHref(source, afterColon, lineEnd, out var hrefBytes, out _))
        {
            return false;
        }

        parsed = new(NormalizeLabel(label), hrefBytes);
        return true;
    }

    /// <summary>Parses the <c>[label]:</c> prefix of a definition line.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="lineStart">Inclusive line start.</param>
    /// <param name="lineEnd">Exclusive line end.</param>
    /// <param name="label">Trimmed label bytes on success.</param>
    /// <param name="afterColon">Cursor position just after the colon (and any trailing inline whitespace) on success.</param>
    /// <returns>True when the prefix is well-formed and a non-empty label was found.</returns>
    private static bool TryParseDefinitionLabel(ReadOnlySpan<byte> source, int lineStart, int lineEnd, out ReadOnlySpan<byte> label, out int afterColon)
    {
        label = default;
        afterColon = 0;
        var p = SkipIndent(source, lineStart, lineEnd, maxIndent: 3);
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
    private static bool TryParseDefinitionHref(ReadOnlySpan<byte> source, int start, int lineEnd, out ReadOnlySpan<byte> hrefBytes, out int afterHref)
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
    private static int ScanAngleBracketedHref(ReadOnlySpan<byte> source, int start, int lineEnd, out bool sawAngleBrackets)
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
        var consumed = 0;
        while (p < lineEnd && consumed < maxIndent && source[p] is (byte)' ')
        {
            p++;
            consumed++;
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
        while (p < lineEnd && source[p] is (byte)' ' or (byte)'\t')
        {
            p++;
        }

        return p;
    }

    /// <summary>Locates the matching close bracket; respects nested pairs and backslash escapes.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="openIndex">Index of the opening bracket.</param>
    /// <returns>Index of the close, or <c>-1</c>.</returns>
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

    /// <summary>Builds the case-folded ASCII key used as the dictionary lookup, collapsing internal whitespace runs to a single space.</summary>
    /// <param name="label">Raw label bytes (already outer-trimmed).</param>
    /// <returns>Normalised string key.</returns>
    private static string NormalizeLabel(ReadOnlySpan<byte> label)
    {
        Span<char> buf = stackalloc char[label.Length];
        var written = 0;
        var prevSpace = false;
        for (var i = 0; i < label.Length; i++)
        {
            var b = label[i];
            if (AsciiByteHelpers.IsAsciiWhitespace(b))
            {
                AppendCollapsedSpace(buf, ref written, ref prevSpace);
                continue;
            }

            prevSpace = false;
            buf[written++] = AsciiByteHelpers.ToAsciiLowerChar(b);
        }

        if (written > 0 && buf[written - 1] is ' ')
        {
            written--;
        }

        return new(buf[..written]);
    }

    /// <summary>Appends at most one space to <paramref name="buf"/> for a run of whitespace bytes.</summary>
    /// <param name="buf">Destination buffer.</param>
    /// <param name="written">Current write index; advanced on emit.</param>
    /// <param name="prevSpace">Tracks whether the previous emitted character was the collapsed space.</param>
    private static void AppendCollapsedSpace(Span<char> buf, ref int written, ref bool prevSpace)
    {
        if (prevSpace || written is 0)
        {
            return;
        }

        buf[written++] = ' ';
        prevSpace = true;
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

    /// <summary>Stored definition entry — owned UTF-8 href bytes.</summary>
    /// <param name="Href">URL bytes (without surrounding angle brackets).</param>
    private readonly record struct Definition(byte[] Href);

    /// <summary>Transient parse result used during the collection pass.</summary>
    private readonly ref struct ParsedDefinition
    {
        /// <summary>Initializes a new instance of the <see cref="ParsedDefinition"/> struct.</summary>
        /// <param name="key">Case-folded label key.</param>
        /// <param name="href">Href bytes.</param>
        public ParsedDefinition(string key, ReadOnlySpan<byte> href)
        {
            Key = key;
            Href = href;
        }

        /// <summary>Gets the case-folded label key.</summary>
        public string Key { get; }

        /// <summary>Gets the href byte slice into the source.</summary>
        public ReadOnlySpan<byte> Href { get; }
    }
}
