// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Reusable byte-span matcher primitives shared by the language lexers.
/// </summary>
/// <remarks>
/// Every helper takes a <see cref="ReadOnlySpan{Byte}"/> anchored at the
/// lexer cursor and returns the number of UTF-8 bytes matched, or
/// <c>0</c> on no match. Returning <c>0</c> tells <see cref="Lexer"/> to
/// fall through to the next rule.
/// <para>
/// Covers patterns that recur across the language lexers — whitespace
/// runs, line comments, identifiers, integer / float literals, simple
/// quoted strings, single-character punctuation against a
/// <see cref="SearchValues{T}"/>, and keyword-set lookups against a
/// <see cref="ByteKeywordSet"/>. Lexer-specific shapes (e.g. Razor
/// transitions, YAML block scalars) live alongside their lexer.
/// </para>
/// </remarks>
public static class TokenMatchers
{
    /// <summary>ASCII whitespace + newlines (<c>' '</c>, <c>'\t'</c>, <c>'\r'</c>, <c>'\n'</c>).</summary>
    public static readonly SearchValues<byte> AsciiWhitespaceWithNewlines = SearchValues.Create(" \t\r\n"u8);

    /// <summary>ASCII inline whitespace (<c>' '</c>, <c>'\t'</c>) — newlines are excluded so callers can treat <c>\n</c> as a token boundary.</summary>
    public static readonly SearchValues<byte> AsciiInlineWhitespace = SearchValues.Create(" \t"u8);

    /// <summary>Line-terminator bytes (<c>'\r'</c>, <c>'\n'</c>).</summary>
    public static readonly SearchValues<byte> LineTerminators = SearchValues.Create("\r\n"u8);

    /// <summary>ASCII digits 0-9.</summary>
    public static readonly SearchValues<byte> AsciiDigits = SearchValues.Create("0123456789"u8);

    /// <summary>Hex digits 0-9 a-f A-F.</summary>
    public static readonly SearchValues<byte> AsciiHexDigits = SearchValues.Create("0123456789abcdefABCDEF"u8);

    /// <summary>ASCII identifier-start bytes (letters + underscore).</summary>
    public static readonly SearchValues<byte> AsciiIdentifierStart = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_"u8);

    /// <summary>ASCII identifier-continuation bytes (letters + digits + underscore).</summary>
    public static readonly SearchValues<byte> AsciiIdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_"u8);

    /// <summary>Matches a run of ASCII whitespace (with newlines). Equivalent to <c>\G[ \t\r\n]+</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the run, or <c>0</c> when the cursor byte isn't whitespace.</returns>
    public static int MatchAsciiWhitespace(ReadOnlySpan<byte> slice) =>
        MatchRunOf(slice, AsciiWhitespaceWithNewlines);

    /// <summary>Matches a run of ASCII inline whitespace (no newlines). Equivalent to <c>\G[ \t]+</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the run, or <c>0</c>.</returns>
    public static int MatchAsciiInlineWhitespace(ReadOnlySpan<byte> slice) =>
        MatchRunOf(slice, AsciiInlineWhitespace);

    /// <summary>Matches a comment that runs from a single prefix byte to the end of the line. Equivalent to <c>\G{prefix}[^\r\n]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix">Comment-introducer byte.</param>
    /// <returns>Length matched, or <c>0</c> when the cursor isn't on <paramref name="prefix"/>.</returns>
    public static int MatchLineCommentToEol(ReadOnlySpan<byte> slice, byte prefix)
    {
        if (slice is [] || slice[0] != prefix)
        {
            return 0;
        }

        var rest = slice[1..];
        var nl = rest.IndexOfAny(LineTerminators);
        return nl < 0 ? slice.Length : 1 + nl;
    }

    /// <summary>Matches a comment that runs from a fixed two-byte prefix to the end of the line. Equivalent to <c>\G{prefix0}{prefix1}[^\r\n]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix0">First prefix byte.</param>
    /// <param name="prefix1">Second prefix byte.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchLineCommentToEol(ReadOnlySpan<byte> slice, byte prefix0, byte prefix1)
    {
        if (slice.Length < 2 || slice[0] != prefix0 || slice[1] != prefix1)
        {
            return 0;
        }

        const int PrefixLength = 2;
        var rest = slice[PrefixLength..];
        var nl = rest.IndexOfAny(LineTerminators);
        return nl < 0 ? slice.Length : PrefixLength + nl;
    }

    /// <summary>Matches an ASCII identifier — one start byte then any number of continue bytes. Equivalent to <c>\G[A-Za-z_][A-Za-z0-9_]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchAsciiIdentifier(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || !AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var rest = slice[1..];
        var stop = rest.IndexOfAnyExcept(AsciiIdentifierContinue);
        return stop < 0 ? slice.Length : 1 + stop;
    }

    /// <summary>Matches an ASCII identifier with caller-supplied start + continue byte classes.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="startSet">Allowed first bytes.</param>
    /// <param name="continueSet">Allowed continuation bytes.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchIdentifier(ReadOnlySpan<byte> slice, SearchValues<byte> startSet, SearchValues<byte> continueSet)
    {
        ArgumentNullException.ThrowIfNull(startSet);
        ArgumentNullException.ThrowIfNull(continueSet);
        if (slice is [] || !startSet.Contains(slice[0]))
        {
            return 0;
        }

        var rest = slice[1..];
        var stop = rest.IndexOfAnyExcept(continueSet);
        return stop < 0 ? slice.Length : 1 + stop;
    }

    /// <summary>Matches one or more ASCII digits. Equivalent to <c>\G[0-9]+</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchAsciiDigits(ReadOnlySpan<byte> slice) => MatchRunOf(slice, AsciiDigits);

    /// <summary>Matches an unsigned ASCII float — at least one digit, a dot, at least one digit, optional <c>e/E</c> exponent. Equivalent to <c>\G\d+\.\d+(?:[eE][+-]?\d+)?</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchUnsignedAsciiFloat(ReadOnlySpan<byte> slice) => slice is [] || !AsciiDigits.Contains(slice[0]) ? 0 : MatchSignedAsciiFloat(slice);

    /// <summary>Matches a body run of <paramref name="bodySet"/> followed by zero or more bytes from <paramref name="suffixSet"/>. Equivalent to <c>\G[bodySet]+[suffixSet]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="bodySet">Required body bytes; at least one must be present.</param>
    /// <param name="suffixSet">Optional trailing bytes consumed greedily.</param>
    /// <returns>Length matched on success, <c>0</c> when the body is empty.</returns>
    public static int MatchRunWithSuffix(ReadOnlySpan<byte> slice, SearchValues<byte> bodySet, SearchValues<byte> suffixSet)
    {
        ArgumentNullException.ThrowIfNull(bodySet);
        ArgumentNullException.ThrowIfNull(suffixSet);
        var bodyLen = MatchRunOf(slice, bodySet);
        if (bodyLen is 0)
        {
            return 0;
        }

        var pos = bodyLen;
        while (pos < slice.Length && suffixSet.Contains(slice[pos]))
        {
            pos++;
        }

        return pos;
    }

    /// <summary>Matches a hex literal — <c>0x</c> / <c>0X</c> followed by a non-empty body of hex digits (with <c>_</c> separators allowed) and an optional suffix run.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="hexBody">Set of allowed hex-body bytes (typically hex digits + <c>_</c>).</param>
    /// <param name="suffixSet">Optional trailing bytes consumed greedily.</param>
    /// <returns>Length matched on success, <c>0</c> on miss.</returns>
    public static int MatchAsciiHexLiteral(ReadOnlySpan<byte> slice, SearchValues<byte> hexBody, SearchValues<byte> suffixSet)
    {
        ArgumentNullException.ThrowIfNull(hexBody);
        ArgumentNullException.ThrowIfNull(suffixSet);
        if (slice.Length < 3 || slice[0] is not (byte)'0' || slice[1] is not ((byte)'x' or (byte)'X'))
        {
            return 0;
        }

        var bodyStop = slice[2..].IndexOfAnyExcept(hexBody);
        var bodyLen = bodyStop < 0 ? slice.Length - 2 : bodyStop;
        if (bodyLen is 0)
        {
            return 0;
        }

        var pos = 2 + bodyLen;
        while (pos < slice.Length && suffixSet.Contains(slice[pos]))
        {
            pos++;
        }

        return pos;
    }

    /// <summary>Matches a prefix byte followed by one or more bytes from <paramref name="bodySet"/>. Equivalent to <c>\G{prefix}[bodySet]+</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix">Required prefix byte.</param>
    /// <param name="bodySet">Allowed body bytes; at least one must be present.</param>
    /// <returns>Length matched on success, <c>0</c> on miss.</returns>
    public static int MatchPrefixedRun(ReadOnlySpan<byte> slice, byte prefix, SearchValues<byte> bodySet)
    {
        ArgumentNullException.ThrowIfNull(bodySet);
        if (slice is [] || slice[0] != prefix)
        {
            return 0;
        }

        var stop = slice[1..].IndexOfAnyExcept(bodySet);
        var bodyLen = stop < 0 ? slice.Length - 1 : stop;
        return bodyLen is 0 ? 0 : 1 + bodyLen;
    }

    /// <summary>Matches an optionally-signed ASCII integer. Equivalent to <c>\G-?[0-9]+</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchSignedAsciiInteger(ReadOnlySpan<byte> slice)
    {
        if (slice is [])
        {
            return 0;
        }

        var offset = slice[0] is (byte)'-' ? 1 : 0;
        if (offset == slice.Length)
        {
            return 0;
        }

        var digits = MatchRunOf(slice[offset..], AsciiDigits);
        return digits is 0 ? 0 : offset + digits;
    }

    /// <summary>Matches an optionally-signed ASCII float — at least one digit, a dot, at least one digit, optional <c>e/E</c> exponent. Equivalent to <c>\G-?\d+\.\d+(?:[eE][+-]?\d+)?</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchSignedAsciiFloat(ReadOnlySpan<byte> slice)
    {
        var pos = 0;
        if (pos < slice.Length && slice[pos] is (byte)'-')
        {
            pos++;
        }

        var intDigits = MatchRunOf(slice[pos..], AsciiDigits);
        if (intDigits is 0)
        {
            return 0;
        }

        pos += intDigits;
        if (pos >= slice.Length || slice[pos] is not (byte)'.')
        {
            return 0;
        }

        pos++;
        var fracDigits = MatchRunOf(slice[pos..], AsciiDigits);
        if (fracDigits is 0)
        {
            return 0;
        }

        pos += fracDigits;
        return pos + ConsumeExponent(slice[pos..]);
    }

    /// <summary>Matches a single-quoted string with no escape sequences. Equivalent to <c>\G'[^']*'</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchSingleQuotedNoEscape(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'\'')
        {
            return 0;
        }

        const int QuotePairLength = 2;
        var close = slice[1..].IndexOf((byte)'\'');
        return close < 0 ? 0 : QuotePairLength + close;
    }

    /// <summary>Double-quoted string with backslash escapes — <c>"(?:\\.|[^"\\])*"</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchDoubleQuotedWithBackslashEscape(ReadOnlySpan<byte> slice) =>
        MatchQuotedWithBackslashEscape(slice, (byte)'"');

    /// <summary>Hash-prefixed line comment — <c>#</c> to end of line. Equivalent to <c>\G#[^\r\n]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int MatchHashComment(ReadOnlySpan<byte> slice) => MatchLineCommentToEol(slice, (byte)'#');

    /// <summary>Matches a quoted string where the embedded-quote escape is the doubled quote.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="quote">Opening + closing quote byte.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchQuotedDoubledEscape(ReadOnlySpan<byte> slice, byte quote)
    {
        const int DoubledQuoteEscape = 2;
        if (slice is [] || slice[0] != quote)
        {
            return 0;
        }

        var i = 1;
        while (i < slice.Length)
        {
            if (slice[i] == quote)
            {
                if (i + 1 >= slice.Length || slice[i + 1] != quote)
                {
                    return i + 1;
                }

                i += DoubledQuoteEscape;
                continue;
            }

            i++;
        }

        return 0;
    }

    /// <summary>Matches a quoted string with backslash escapes. Equivalent to <c>\G{q}(?:\\.|[^{q}\\])*{q}</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="quote">The opening + closing quote byte (typically <c>(byte)'"'</c>).</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchQuotedWithBackslashEscape(ReadOnlySpan<byte> slice, byte quote)
    {
        const int BackslashEscapeLength = 2;
        if (slice is [] || slice[0] != quote)
        {
            return 0;
        }

        var i = 1;
        while (i < slice.Length)
        {
            var c = slice[i];
            if (c == quote)
            {
                return i + 1;
            }

            if (c is (byte)'\\')
            {
                i += BackslashEscapeLength;
                continue;
            }

            i++;
        }

        return 0;
    }

    /// <summary>
    /// Matches the rest of the current line when the cursor is on
    /// <paramref name="prefix"/>. Equivalent to <c>\G{prefix}[^\r\n]*</c>,
    /// but returning the full line including the prefix.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix">Required first byte of the line.</param>
    /// <returns>Length of the line on match, <c>0</c> on miss.</returns>
    public static int MatchPrefixedLine(ReadOnlySpan<byte> slice, byte prefix) =>
        slice is [_, ..] && slice[0] == prefix ? LineLength(slice) : 0;

    /// <summary>Matches the rest of the current line when the cursor is on the two-byte sequence <paramref name="prefix0"/><paramref name="prefix1"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix0">First required byte.</param>
    /// <param name="prefix1">Second required byte.</param>
    /// <returns>Length of the line on match, <c>0</c> on miss.</returns>
    public static int MatchPrefixedLine(ReadOnlySpan<byte> slice, byte prefix0, byte prefix1) =>
        slice.Length >= 2 && slice[0] == prefix0 && slice[1] == prefix1 ? LineLength(slice) : 0;

    /// <summary>Matches the rest of the current line when the cursor sits on the longest matching <paramref name="prefixes"/> entry.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefixes">Candidate prefixes, sorted by descending length.</param>
    /// <returns>Length of the line on match, <c>0</c> on miss.</returns>
    public static int MatchPrefixedLineLongest(ReadOnlySpan<byte> slice, in ReadOnlySpan<byte[]> prefixes)
    {
        var prefix = MatchLongestLiteral(slice, prefixes);
        return prefix is 0 ? 0 : prefix + LineLength(slice[prefix..]);
    }

    /// <summary>Matches the rest of the current line when the cursor's first byte is <em>not</em> in <paramref name="stopFirstBytes"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="stopFirstBytes">Bytes that disqualify a line.</param>
    /// <returns>Length of the line on match, <c>0</c> on miss.</returns>
    public static int MatchLineUnlessStartsWith(ReadOnlySpan<byte> slice, SearchValues<byte> stopFirstBytes)
    {
        ArgumentNullException.ThrowIfNull(stopFirstBytes);
        return slice is [] || stopFirstBytes.Contains(slice[0]) ? 0 : LineLength(slice);
    }

    /// <summary>Matches a single byte that's a member of <paramref name="set"/>. Equivalent to <c>\G[set]</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="set">Allowed bytes.</param>
    /// <returns><c>1</c> on match, <c>0</c> on miss.</returns>
    public static int MatchSingleByteOf(ReadOnlySpan<byte> slice, SearchValues<byte> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return slice is [] || !set.Contains(slice[0]) ? 0 : 1;
    }

    /// <summary>Matches one of the keywords in <paramref name="keywords"/> followed by a non-identifier-continue byte (or end-of-slice). Equivalent to <c>\G(?:k0|k1|...)\b</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="keywords">Set of candidate keywords.</param>
    /// <returns>Length of the matched keyword, or <c>0</c>.</returns>
    public static int MatchKeyword(ReadOnlySpan<byte> slice, ByteKeywordSet keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        if (slice is [] || !AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var endRel = slice[1..].IndexOfAnyExcept(AsciiIdentifierContinue);
        var end = endRel < 0 ? slice.Length : 1 + endRel;
        return keywords.Contains(slice[..end]) ? end : 0;
    }

    /// <summary>Matches the literal <paramref name="literal"/> at the cursor. Equivalent to <c>\G{literal}</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="literal">Literal to match.</param>
    /// <returns>Length of <paramref name="literal"/> on match, <c>0</c> on miss.</returns>
    public static int MatchLiteral(ReadOnlySpan<byte> slice, ReadOnlySpan<byte> literal) =>
        slice.StartsWith(literal) ? literal.Length : 0;

    /// <summary>
    /// Matches the longest prefix from <paramref name="literals"/> at the cursor.
    /// <paramref name="literals"/> must be sorted longest-first so callers picking from a
    /// candidate set never hit a shorter literal that is a prefix of a longer one.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="literals">Candidate literals, sorted by descending length.</param>
    /// <returns>Length of the longest matching literal, or <c>0</c>.</returns>
    public static int MatchLongestLiteral(ReadOnlySpan<byte> slice, in ReadOnlySpan<byte[]> literals)
    {
        for (var i = 0; i < literals.Length; i++)
        {
            var lit = literals[i];
            if (slice.StartsWith(lit))
            {
                return lit.Length;
            }
        }

        return 0;
    }

    /// <summary>Matches a non-greedy delimited block: <paramref name="open"/> followed by anything up to the first <paramref name="close"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="open">Required opening literal.</param>
    /// <param name="close">Required closing literal.</param>
    /// <returns>Length matched (including both delimiters), or <c>0</c>.</returns>
    public static int MatchDelimited(ReadOnlySpan<byte> slice, ReadOnlySpan<byte> open, ReadOnlySpan<byte> close)
    {
        if (!slice.StartsWith(open))
        {
            return 0;
        }

        var rest = slice[open.Length..];
        var endRel = rest.IndexOf(close);
        return endRel < 0 ? 0 : open.Length + endRel + close.Length;
    }

    /// <summary>Matches a run from the cursor up to (but not including) the first byte in <paramref name="stopSet"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="stopSet">Bytes that terminate the run.</param>
    /// <returns>Length matched on success; <c>0</c> when the cursor is on a stop byte or the slice is empty.</returns>
    public static int MatchRunUntilAny(ReadOnlySpan<byte> slice, SearchValues<byte> stopSet)
    {
        ArgumentNullException.ThrowIfNull(stopSet);
        var stop = slice.IndexOfAny(stopSet);
        return stop switch
        {
            < 0 when slice is [] => 0,
            < 0 => slice.Length,
            0 => 0,
            _ => stop
        };
    }

    /// <summary>Matches a single-quoted string where the embedded-quote escape is the doubled quote (<c>''</c>) rather than a backslash.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchSingleQuotedDoubledEscape(ReadOnlySpan<byte> slice) =>
        MatchQuotedDoubledEscape(slice, (byte)'\'');

    /// <summary>Matches a double-quoted string where the embedded-quote escape is the doubled quote (<c>""</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchDoubleQuotedDoubledEscape(ReadOnlySpan<byte> slice) =>
        MatchQuotedDoubledEscape(slice, (byte)'"');

    /// <summary>Matches a paired block comment with two-byte open and close delimiters (no nesting).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="open">Two-byte opener (e.g. <c>"#="u8</c> for Julia, <c>"%{"u8</c> for MATLAB, <c>"#["u8</c> for Nim).</param>
    /// <param name="close">Two-byte closer (e.g. <c>"=#"u8</c>, <c>"%}"u8</c>, <c>"]#"u8</c>).</param>
    /// <returns>Length matched (including both delimiters), or zero on miss / unterminated input.</returns>
    public static int MatchPairedBlockComment(ReadOnlySpan<byte> slice, ReadOnlySpan<byte> open, ReadOnlySpan<byte> close)
    {
        const int TwoByteDelimiterLength = 2;
        if (open.Length != TwoByteDelimiterLength || close.Length != TwoByteDelimiterLength)
        {
            return 0;
        }

        const int MinClosedLength = TwoByteDelimiterLength + TwoByteDelimiterLength;
        if (slice.Length < MinClosedLength || slice[0] != open[0] || slice[1] != open[1])
        {
            return 0;
        }

        var rest = slice[TwoByteDelimiterLength..];
        var closeAt = rest.IndexOf(close);
        return closeAt < 0 ? 0 : TwoByteDelimiterLength + closeAt + TwoByteDelimiterLength;
    }

    /// <summary>
    /// Matches a bracketed block — required <paramref name="open"/> byte, a body of any bytes except
    /// <paramref name="close"/>, then a required <paramref name="close"/> byte.
    /// Equivalent to <c>\G{open}[^{close}]*{close}</c>.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="open">Required opening byte.</param>
    /// <param name="close">Required closing byte; the body excludes this byte.</param>
    /// <returns>Length matched (including both delimiters), or <c>0</c>.</returns>
    public static int MatchBracketedBlock(ReadOnlySpan<byte> slice, byte open, byte close)
    {
        if (slice is [] || slice[0] != open)
        {
            return 0;
        }

        var rest = slice[1..];
        var endRel = rest.IndexOf(close);
        return endRel < 0 ? 0 : 1 + endRel + 1;
    }

    /// <summary>
    /// Matches a raw string literal — N opening quotes followed by anything
    /// (including newlines) up to a matching N closing quotes that aren't
    /// adjacent to additional quote bytes. Equivalent to C# 11's
    /// <c>"""..."""</c> shape.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="quote">Quote byte (typically <c>(byte)'"'</c>).</param>
    /// <param name="minQuotes">Minimum opening / closing quote run (typically 3 — one or two are caught by other matchers).</param>
    /// <returns>Length matched (including both delimiter runs), or <c>0</c>.</returns>
    public static int MatchRawQuotedString(ReadOnlySpan<byte> slice, byte quote, int minQuotes)
    {
        const int OpenAndClosePairs = 2;
        ArgumentOutOfRangeException.ThrowIfLessThan(minQuotes, 1);
        if (slice.Length < minQuotes * OpenAndClosePairs)
        {
            return 0;
        }

        var openLen = 0;
        while (openLen < slice.Length && slice[openLen] == quote)
        {
            openLen++;
        }

        if (openLen < minQuotes)
        {
            return 0;
        }

        var pos = openLen;
        while (pos < slice.Length)
        {
            if (slice[pos] != quote)
            {
                pos++;
                continue;
            }

            var runStart = pos;
            while (pos < slice.Length && slice[pos] == quote)
            {
                pos++;
            }

            var runLen = pos - runStart;
            if (runLen >= openLen)
            {
                return pos;
            }
        }

        return 0;
    }

    /// <summary>Matches a line terminator — <c>\r\n</c>, a bare <c>\r</c>, or a bare <c>\n</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int MatchNewline(ReadOnlySpan<byte> slice)
    {
        const int CrLfLength = 2;
        return slice switch
        {
            [(byte)'\r', (byte)'\n', ..] => CrLfLength,
            [(byte)'\r', ..] or [(byte)'\n', ..] => 1,
            _ => 0
        };
    }

    /// <summary>Matches a double-quoted string with backslash escapes followed by optional whitespace and a <c>:</c> lookahead — the property-key shape used by JSON and YAML.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the string literal on match (the colon is left for the punctuation rule).</returns>
    public static int MatchDoubleQuotedKey(ReadOnlySpan<byte> slice)
    {
        var stringLen = MatchDoubleQuotedWithBackslashEscape(slice);
        if (stringLen is 0)
        {
            return 0;
        }

        var ws = MatchAsciiWhitespace(slice[stringLen..]);
        return stringLen + ws < slice.Length && slice[stringLen + ws] is (byte)':' ? stringLen : 0;
    }

    /// <summary>Returns the length of the current line measured from <paramref name="slice"/>'s start (excluding the terminator).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length up to the next <c>\r</c> or <c>\n</c>, or <paramref name="slice"/>'s full length when no terminator is present.</returns>
    public static int LineLength(ReadOnlySpan<byte> slice)
    {
        var nl = slice.IndexOfAny(LineTerminators);
        return nl < 0 ? slice.Length : nl;
    }

    /// <summary>Returns the length of the run starting at the cursor where every byte is a member of <paramref name="set"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="set">Allowed bytes.</param>
    /// <returns>Length of the run, or <c>0</c> when the cursor byte isn't in <paramref name="set"/>.</returns>
    public static int MatchRunOf(ReadOnlySpan<byte> slice, SearchValues<byte> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var stop = slice.IndexOfAnyExcept(set);
        return stop switch
        {
            < 0 when slice is [] => 0,
            < 0 => slice.Length,
            0 => 0,
            _ => stop
        };
    }

    /// <summary>Consumes an optional <c>e</c>/<c>E</c> exponent (with optional sign and required digits) at the start of <paramref name="slice"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length consumed, or <c>0</c> when no valid exponent is present.</returns>
    private static int ConsumeExponent(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not ((byte)'e' or (byte)'E'))
        {
            return 0;
        }

        var pos = 1;
        if (pos < slice.Length && slice[pos] is (byte)'+' or (byte)'-')
        {
            pos++;
        }

        var digits = MatchRunOf(slice[pos..], AsciiDigits);
        return digits is 0 ? 0 : pos + digits;
    }
}
