// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Layouts;

/// <summary>Tokenizes a layout template into a flat <see cref="LayoutToken"/> stream.</summary>
internal static class LayoutScanner
{
    /// <summary>Length of a two-byte open / close marker (<c>{{</c>, <c>}}</c>, <c>{%</c>, <c>%}</c>).</summary>
    private const int MarkerLength = 2;

    /// <summary>Scans <paramref name="template"/> and writes every token into <paramref name="output"/>.</summary>
    /// <param name="template">UTF-8 template bytes.</param>
    /// <param name="output">Destination list.</param>
    public static void Scan(ReadOnlySpan<byte> template, List<LayoutToken> output)
    {
        var cursor = 0;
        var literalStart = 0;
        while (cursor < template.Length)
        {
            if (template[cursor] is not (byte)'{' || cursor + 1 >= template.Length)
            {
                cursor++;
                continue;
            }

            var second = template[cursor + 1];
            if (second is not ((byte)'{' or (byte)'%'))
            {
                cursor++;
                continue;
            }

            FlushLiteral(literalStart, cursor, output);
            cursor = second is (byte)'{'
                ? ScanVariable(template, cursor, output)
                : ScanTag(template, cursor, output);
            literalStart = cursor;
        }

        FlushLiteral(literalStart, template.Length, output);
    }

    /// <summary>Emits a <see cref="LayoutTokenKind.Literal"/> covering <paramref name="start"/>..<paramref name="end"/> when non-empty.</summary>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <param name="output">Destination list.</param>
    private static void FlushLiteral(int start, int end, List<LayoutToken> output)
    {
        if (end <= start)
        {
            return;
        }

        output.Add(new(LayoutTokenKind.Literal, start, end, start, end));
    }

    /// <summary>Scans a <c>{{ … }}</c> variable marker.</summary>
    /// <param name="template">UTF-8 source.</param>
    /// <param name="open">Offset of the leading <c>{</c>.</param>
    /// <param name="output">Destination list.</param>
    /// <returns>Resume offset just past the closing marker on success, or one past <paramref name="open"/> on malformed input.</returns>
    private static int ScanVariable(ReadOnlySpan<byte> template, int open, List<LayoutToken> output)
    {
        var bodyStart = open + MarkerLength;
        var rel = template[bodyStart..].IndexOf("}}"u8);
        if (rel < 0)
        {
            output.Add(new(LayoutTokenKind.Malformed, open, open + 1, open, open + 1));
            return open + 1;
        }

        var bodyEnd = bodyStart + rel;
        var end = bodyEnd + MarkerLength;
        var (nameStart, nameEnd) = TrimRange(template, bodyStart, bodyEnd);
        var name = template[nameStart..nameEnd];
        if (name.SequenceEqual("super()"u8))
        {
            output.Add(new(LayoutTokenKind.Super, open, end, open, end));
            return end;
        }

        if (TryStripPagePrefix(name, out var bare))
        {
            var bareStart = nameEnd - bare.Length;
            output.Add(new(LayoutTokenKind.Variable, open, end, bareStart, nameEnd));
            return end;
        }

        output.Add(new(LayoutTokenKind.Malformed, open, end, nameStart, nameEnd));
        return end;
    }

    /// <summary>Scans a <c>{% … %}</c> tag.</summary>
    /// <param name="template">UTF-8 source.</param>
    /// <param name="open">Offset of the leading <c>{</c>.</param>
    /// <param name="output">Destination list.</param>
    /// <returns>Resume offset just past the closing marker on success, or one past <paramref name="open"/> on malformed input.</returns>
    private static int ScanTag(ReadOnlySpan<byte> template, int open, List<LayoutToken> output)
    {
        var bodyStart = open + MarkerLength;
        var rel = template[bodyStart..].IndexOf("%}"u8);
        if (rel < 0)
        {
            output.Add(new(LayoutTokenKind.Malformed, open, open + 1, open, open + 1));
            return open + 1;
        }

        var bodyEnd = bodyStart + rel;
        var end = bodyEnd + MarkerLength;
        var (trimStart, trimEnd) = TrimRange(template, bodyStart, bodyEnd);
        if (trimStart >= trimEnd)
        {
            output.Add(new(LayoutTokenKind.Unsupported, open, end, bodyStart, bodyEnd));
            return end;
        }

        output.Add(ClassifyTag(template, trimStart, trimEnd, open, end, bodyStart, bodyEnd));
        return end;
    }

    /// <summary>Classifies the trimmed body of a <c>{% … %}</c> tag.</summary>
    /// <param name="template">UTF-8 template.</param>
    /// <param name="trimStart">Inclusive trimmed-body start.</param>
    /// <param name="trimEnd">Exclusive trimmed-body end.</param>
    /// <param name="open">Token start (inclusive of <c>{%</c>).</param>
    /// <param name="end">Token end (exclusive of <c>%}</c>).</param>
    /// <param name="bodyStart">Inclusive raw-body start in <paramref name="template"/>.</param>
    /// <param name="bodyEnd">Exclusive raw-body end in <paramref name="template"/>.</param>
    /// <returns>The classified token.</returns>
    private static LayoutToken ClassifyTag(ReadOnlySpan<byte> template, int trimStart, int trimEnd, int open, int end, int bodyStart, int bodyEnd)
    {
        var body = template[trimStart..trimEnd];
        if (body.SequenceEqual("endblock"u8))
        {
            return new(LayoutTokenKind.BlockClose, open, end, open, end);
        }

        if (TryFindQuotedTarget(template, body, trimStart, trimEnd, "extends"u8, out var extS, out var extE))
        {
            return new(LayoutTokenKind.Extends, open, end, extS, extE);
        }

        if (TryFindQuotedTarget(template, body, trimStart, trimEnd, "include"u8, out var incS, out var incE))
        {
            return new(LayoutTokenKind.Include, open, end, incS, incE);
        }

        if (TryFindBlockName(template, body, trimStart, trimEnd, out var bnS, out var bnE))
        {
            return new(LayoutTokenKind.BlockOpen, open, end, bnS, bnE);
        }

        return new(LayoutTokenKind.Unsupported, open, end, bodyStart, bodyEnd);
    }

    /// <summary>Tries to extract the unquoted target of a <c>{% keyword "target" %}</c> tag.</summary>
    /// <param name="template">UTF-8 template.</param>
    /// <param name="body">Trimmed body bytes.</param>
    /// <param name="trimStart">Inclusive trimmed-body start.</param>
    /// <param name="trimEnd">Exclusive trimmed-body end.</param>
    /// <param name="keyword">Tag keyword bytes.</param>
    /// <param name="targetStart">Inner start on hit.</param>
    /// <param name="targetEnd">Inner end on hit.</param>
    /// <returns>True when the body matched and yielded a non-empty target.</returns>
    private static bool TryFindQuotedTarget(
        ReadOnlySpan<byte> template,
        ReadOnlySpan<byte> body,
        int trimStart,
        int trimEnd,
        ReadOnlySpan<byte> keyword,
        out int targetStart,
        out int targetEnd)
    {
        targetStart = 0;
        targetEnd = 0;
        return TryStripKeyword(body, keyword, out var consumed)
            && TryUnquoteRange(template, trimStart + consumed, trimEnd, out targetStart, out targetEnd);
    }

    /// <summary>Tries to extract the bare identifier of a <c>{% block name %}</c> tag.</summary>
    /// <param name="template">UTF-8 template.</param>
    /// <param name="body">Trimmed body bytes.</param>
    /// <param name="trimStart">Inclusive trimmed-body start.</param>
    /// <param name="trimEnd">Exclusive trimmed-body end.</param>
    /// <param name="nameStart">Identifier start on hit.</param>
    /// <param name="nameEnd">Identifier end on hit.</param>
    /// <returns>True when the body matched and the trailing identifier is bare.</returns>
    private static bool TryFindBlockName(
        ReadOnlySpan<byte> template,
        ReadOnlySpan<byte> body,
        int trimStart,
        int trimEnd,
        out int nameStart,
        out int nameEnd)
    {
        nameStart = 0;
        nameEnd = 0;
        if (!TryStripKeyword(body, "block"u8, out var consumed))
        {
            return false;
        }

        (nameStart, nameEnd) = TrimRange(template, trimStart + consumed, trimEnd);
        return nameStart < nameEnd && IsBareIdentifier(template[nameStart..nameEnd]);
    }

    /// <summary>Strips a leading keyword and any whitespace separating it from the rest.</summary>
    /// <param name="body">Trimmed tag body.</param>
    /// <param name="keyword">Keyword bytes.</param>
    /// <param name="consumed">Number of bytes consumed (keyword + at least one whitespace byte) on success.</param>
    /// <returns>True when the body started with the keyword followed by whitespace.</returns>
    private static bool TryStripKeyword(ReadOnlySpan<byte> body, ReadOnlySpan<byte> keyword, out int consumed)
    {
        consumed = 0;
        if (!body.StartsWith(keyword) || body.Length <= keyword.Length)
        {
            return false;
        }

        var sep = body[keyword.Length];
        if (sep is not ((byte)' ' or (byte)'\t'))
        {
            return false;
        }

        consumed = keyword.Length + 1;
        return true;
    }

    /// <summary>Trims, then strips matching <c>"…"</c> / <c>'…'</c> quotes from a sub-range of <paramref name="template"/>.</summary>
    /// <param name="template">UTF-8 template.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <param name="innerStart">Inclusive inner start on success.</param>
    /// <param name="innerEnd">Exclusive inner end on success.</param>
    /// <returns>True when the trimmed range is wrapped in matching quotes and the inner is non-empty.</returns>
    private static bool TryUnquoteRange(ReadOnlySpan<byte> template, int start, int end, out int innerStart, out int innerEnd)
    {
        innerStart = 0;
        innerEnd = 0;
        var (s, e) = TrimRange(template, start, end);
        if (e - s < MarkerLength)
        {
            return false;
        }

        var first = template[s];
        var last = template[e - 1];
        var matched = (first is (byte)'"' && last is (byte)'"') || (first is (byte)'\'' && last is (byte)'\'');
        if (!matched)
        {
            return false;
        }

        innerStart = s + 1;
        innerEnd = e - 1;
        return innerEnd > innerStart;
    }

    /// <summary>Trims ASCII whitespace from a sub-range and returns the resulting offsets.</summary>
    /// <param name="template">UTF-8 template.</param>
    /// <param name="start">Inclusive start.</param>
    /// <param name="end">Exclusive end.</param>
    /// <returns>Trimmed (start, end) offsets.</returns>
    private static (int Start, int End) TrimRange(ReadOnlySpan<byte> template, int start, int end)
    {
        while (start < end && AsciiByteHelpers.IsAsciiWhitespace(template[start]))
        {
            start++;
        }

        while (end > start && AsciiByteHelpers.IsAsciiWhitespace(template[end - 1]))
        {
            end--;
        }

        return (start, end);
    }

    /// <summary>Detects the <c>page.</c> prefix and returns the bare name slice.</summary>
    /// <param name="span">Trimmed variable expression.</param>
    /// <param name="bare">Bare name (after the prefix) on success.</param>
    /// <returns>True when the expression starts with <c>page.</c> and the remainder is a bare identifier.</returns>
    private static bool TryStripPagePrefix(ReadOnlySpan<byte> span, out ReadOnlySpan<byte> bare)
    {
        bare = default;
        if (!span.StartsWith("page."u8) || span.Length <= "page.".Length)
        {
            return false;
        }

        var rest = span["page.".Length..];
        if (!IsBareIdentifier(rest))
        {
            return false;
        }

        bare = rest;
        return true;
    }

    /// <summary>True when every byte of <paramref name="span"/> is a letter, digit, dot, underscore, or hyphen.</summary>
    /// <param name="span">Candidate identifier bytes.</param>
    /// <returns>True for valid identifiers.</returns>
    private static bool IsBareIdentifier(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
        {
            return false;
        }

        for (var i = 0; i < span.Length; i++)
        {
            if (!IsIdentifierByte(span[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True for ASCII bytes that may appear inside a bare identifier (letters, digits, dot, underscore, hyphen).</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for legal identifier bytes.</returns>
    private static bool IsIdentifierByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'.'
          or (byte)'_'
          or (byte)'-';
}
