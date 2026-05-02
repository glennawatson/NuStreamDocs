// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;

namespace NuStreamDocs.Highlight;

/// <summary>
/// Reusable matcher primitives shared by the language lexers.
/// </summary>
/// <remarks>
/// Every helper takes a <see cref="ReadOnlySpan{T}"/> anchored at the
/// lexer cursor and returns the number of characters matched, or
/// <c>0</c> on no match. Returning <c>0</c> tells <see cref="Lexer"/>
/// to fall through to the next rule.
/// <para>
/// Covers patterns that recur across the language lexers —
/// whitespace runs, line comments, identifiers, integer / float
/// literals, simple quoted strings, single-character punctuation
/// against a <see cref="SearchValues{T}"/>, and keyword-set lookups.
/// Lexer-specific shapes (e.g. Razor transitions, YAML block
/// scalars) live alongside their lexer.
/// </para>
/// </remarks>
public static class TokenMatchers
{
    /// <summary>ASCII whitespace + newlines (<c>' '</c>, <c>'\t'</c>, <c>'\r'</c>, <c>'\n'</c>).</summary>
    public static readonly SearchValues<char> AsciiWhitespaceWithNewlines = SearchValues.Create(" \t\r\n");

    /// <summary>ASCII inline whitespace (<c>' '</c>, <c>'\t'</c>) — newlines are excluded so callers can treat <c>\n</c> as a token boundary.</summary>
    public static readonly SearchValues<char> AsciiInlineWhitespace = SearchValues.Create(" \t");

    /// <summary>Line-terminator characters (<c>'\r'</c>, <c>'\n'</c>).</summary>
    public static readonly SearchValues<char> LineTerminators = SearchValues.Create("\r\n");

    /// <summary>ASCII digits 0-9.</summary>
    public static readonly SearchValues<char> AsciiDigits = SearchValues.Create("0123456789");

    /// <summary>Hex digits 0-9 a-f A-F.</summary>
    public static readonly SearchValues<char> AsciiHexDigits = SearchValues.Create("0123456789abcdefABCDEF");

    /// <summary>ASCII identifier-start characters (letters + underscore).</summary>
    public static readonly SearchValues<char> AsciiIdentifierStart = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_");

    /// <summary>ASCII identifier-continuation characters (letters + digits + underscore).</summary>
    public static readonly SearchValues<char> AsciiIdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_");

    /// <summary>Matches a run of ASCII whitespace (with newlines). Equivalent to <c>\G[ \t\r\n]+</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the run, or <c>0</c> when the cursor character isn't whitespace.</returns>
    public static int MatchAsciiWhitespace(ReadOnlySpan<char> slice) =>
        MatchRunOf(slice, AsciiWhitespaceWithNewlines);

    /// <summary>Matches a run of ASCII inline whitespace (no newlines). Equivalent to <c>\G[ \t]+</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the run, or <c>0</c>.</returns>
    public static int MatchAsciiInlineWhitespace(ReadOnlySpan<char> slice) =>
        MatchRunOf(slice, AsciiInlineWhitespace);

    /// <summary>Matches a comment that runs from a single prefix character to the end of the line. Equivalent to <c>\G{prefix}[^\r\n]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix">Comment-introducer character (e.g. <c>'#'</c> for shell, <c>'/'</c> in <c>//</c> handled separately).</param>
    /// <returns>Length matched, or <c>0</c> when the cursor isn't on <paramref name="prefix"/>.</returns>
    public static int MatchLineCommentToEol(ReadOnlySpan<char> slice, char prefix)
    {
        if (slice is [] || slice[0] != prefix)
        {
            return 0;
        }

        var rest = slice[1..];
        var nl = rest.IndexOfAny(LineTerminators);
        return nl < 0 ? slice.Length : 1 + nl;
    }

    /// <summary>Matches a comment that runs from a fixed two-character prefix to the end of the line. Equivalent to <c>\G{prefix0}{prefix1}[^\r\n]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix0">First prefix character (e.g. <c>'/'</c>).</param>
    /// <param name="prefix1">Second prefix character (e.g. <c>'/'</c>).</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchLineCommentToEol(ReadOnlySpan<char> slice, char prefix0, char prefix1)
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

    /// <summary>Matches an ASCII identifier — one start char then any number of continue chars. Equivalent to <c>\G[A-Za-z_][A-Za-z0-9_]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchAsciiIdentifier(ReadOnlySpan<char> slice)
    {
        if (slice is [] || !AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var rest = slice[1..];
        var stop = rest.IndexOfAnyExcept(AsciiIdentifierContinue);
        return stop < 0 ? slice.Length : 1 + stop;
    }

    /// <summary>Matches an ASCII identifier with caller-supplied start + continue character classes.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="startSet">Allowed first characters.</param>
    /// <param name="continueSet">Allowed continuation characters.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchIdentifier(ReadOnlySpan<char> slice, SearchValues<char> startSet, SearchValues<char> continueSet)
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
    public static int MatchAsciiDigits(ReadOnlySpan<char> slice) => MatchRunOf(slice, AsciiDigits);

    /// <summary>Matches an unsigned ASCII float — at least one digit, a dot, at least one digit, optional <c>e/E</c> exponent. Equivalent to <c>\G\d+\.\d+(?:[eE][+-]?\d+)?</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or <c>0</c>.</returns>
    public static int MatchUnsignedAsciiFloat(ReadOnlySpan<char> slice)
    {
        if (slice is [] || !AsciiDigits.Contains(slice[0]))
        {
            return 0;
        }

        return MatchSignedAsciiFloat(slice);
    }

    /// <summary>Matches a body run of <paramref name="bodySet"/> followed by zero or more characters from <paramref name="suffixSet"/>. Equivalent to <c>\G[bodySet]+[suffixSet]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="bodySet">Required body characters; at least one must be present.</param>
    /// <param name="suffixSet">Optional trailing characters consumed greedily.</param>
    /// <returns>Length matched on success, <c>0</c> when the body is empty.</returns>
    public static int MatchRunWithSuffix(ReadOnlySpan<char> slice, SearchValues<char> bodySet, SearchValues<char> suffixSet)
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
    /// <param name="hexBody">Set of allowed hex-body characters (typically hex digits + <c>_</c>).</param>
    /// <param name="suffixSet">Optional trailing characters consumed greedily.</param>
    /// <returns>Length matched on success, <c>0</c> on miss.</returns>
    public static int MatchAsciiHexLiteral(ReadOnlySpan<char> slice, SearchValues<char> hexBody, SearchValues<char> suffixSet)
    {
        ArgumentNullException.ThrowIfNull(hexBody);
        ArgumentNullException.ThrowIfNull(suffixSet);
        if (slice.Length < 3 || slice[0] is not '0' || slice[1] is not ('x' or 'X'))
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

    /// <summary>Matches a prefix character followed by one or more characters from <paramref name="bodySet"/>. Equivalent to <c>\G{prefix}[bodySet]+</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix">Required prefix character.</param>
    /// <param name="bodySet">Allowed body characters; at least one must be present.</param>
    /// <returns>Length matched on success, <c>0</c> on miss.</returns>
    public static int MatchPrefixedRun(ReadOnlySpan<char> slice, char prefix, SearchValues<char> bodySet)
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
    public static int MatchSignedAsciiInteger(ReadOnlySpan<char> slice)
    {
        if (slice is [])
        {
            return 0;
        }

        var offset = slice[0] is '-' ? 1 : 0;
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
    public static int MatchSignedAsciiFloat(ReadOnlySpan<char> slice)
    {
        var pos = 0;
        if (pos < slice.Length && slice[pos] is '-')
        {
            pos++;
        }

        var intDigits = MatchRunOf(slice[pos..], AsciiDigits);
        if (intDigits is 0)
        {
            return 0;
        }

        pos += intDigits;
        if (pos >= slice.Length || slice[pos] is not '.')
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
    public static int MatchSingleQuotedNoEscape(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is not '\'')
        {
            return 0;
        }

        const int QuotePairLength = 2;
        var close = slice[1..].IndexOf('\'');
        return close < 0 ? 0 : QuotePairLength + close;
    }

    /// <summary>Double-quoted string with backslash escapes — <c>"(?:\\.|[^"\\])*"</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchDoubleQuotedWithBackslashEscape(ReadOnlySpan<char> slice) =>
        MatchQuotedWithBackslashEscape(slice, '"');

    /// <summary>Hash-prefixed line comment — <c>#</c> to end of line. Equivalent to <c>\G#[^\r\n]*</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int MatchHashComment(ReadOnlySpan<char> slice) => MatchLineCommentToEol(slice, '#');

    /// <summary>Matches a quoted string where the embedded-quote escape is the doubled quote.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="quote">Opening + closing quote character.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchQuotedDoubledEscape(ReadOnlySpan<char> slice, char quote)
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
                if (i + 1 < slice.Length && slice[i + 1] == quote)
                {
                    i += DoubledQuoteEscape;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return 0;
    }

    /// <summary>Matches a quoted string with backslash escapes. Equivalent to <c>\G{q}(?:\\.|[^{q}\\])*{q}</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="quote">The opening + closing quote character (typically <c>'"'</c>).</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchQuotedWithBackslashEscape(ReadOnlySpan<char> slice, char quote)
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

            if (c is '\\')
            {
                i += BackslashEscapeLength;
                continue;
            }

            i++;
        }

        return 0;
    }

    /// <summary>Matches a single character that's a member of <paramref name="set"/>. Equivalent to <c>\G[set]</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="set">Allowed characters.</param>
    /// <returns><c>1</c> on match, <c>0</c> on miss.</returns>
    public static int MatchSingleCharOf(ReadOnlySpan<char> slice, SearchValues<char> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        return slice is [] || !set.Contains(slice[0]) ? 0 : 1;
    }

    /// <summary>Matches one of the keywords in <paramref name="keywords"/> followed by a non-identifier-continue character (or end-of-slice). Equivalent to <c>\G(?:k0|k1|...)\b</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="keywords">Set of candidate keywords.</param>
    /// <returns>Length of the matched keyword, or <c>0</c>.</returns>
    public static int MatchKeyword(ReadOnlySpan<char> slice, FrozenSet<string> keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        if (slice is [] || !AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var endRel = slice[1..].IndexOfAnyExcept(AsciiIdentifierContinue);
        var end = endRel < 0 ? slice.Length : 1 + endRel;
        var word = slice[..end];
        var lookup = keywords.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.Contains(word) ? end : 0;
    }

    /// <summary>Matches one of the keywords in <paramref name="keywords"/> using case-insensitive comparison, followed by a non-identifier-continue boundary.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="keywords">Set of candidate keywords; the set's comparer must be case-insensitive.</param>
    /// <returns>Length of the matched keyword, or <c>0</c>.</returns>
    public static int MatchKeywordIgnoreCase(ReadOnlySpan<char> slice, FrozenSet<string> keywords)
    {
        ArgumentNullException.ThrowIfNull(keywords);
        if (slice is [] || !AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var endRel = slice[1..].IndexOfAnyExcept(AsciiIdentifierContinue);
        var end = endRel < 0 ? slice.Length : 1 + endRel;
        var lookup = keywords.GetAlternateLookup<ReadOnlySpan<char>>();
        return lookup.Contains(slice[..end]) ? end : 0;
    }

    /// <summary>Matches the literal string <paramref name="literal"/> at the cursor. Equivalent to <c>\G{literal}</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="literal">Literal to match.</param>
    /// <returns>Length of <paramref name="literal"/> on match, <c>0</c> on miss.</returns>
    public static int MatchLiteral(ReadOnlySpan<char> slice, ReadOnlySpan<char> literal) =>
        slice.StartsWith(literal) ? literal.Length : 0;

    /// <summary>
    /// Matches the longest prefix from <paramref name="literals"/> at the cursor.
    /// <paramref name="literals"/> must be sorted longest-first so callers picking from a
    /// candidate set never hit a shorter literal that is a prefix of a longer one.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="literals">Candidate literals, sorted by descending length.</param>
    /// <returns>Length of the longest matching literal, or <c>0</c>.</returns>
    public static int MatchLongestLiteral(ReadOnlySpan<char> slice, ReadOnlySpan<string> literals)
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
    public static int MatchDelimited(ReadOnlySpan<char> slice, ReadOnlySpan<char> open, ReadOnlySpan<char> close)
    {
        if (!slice.StartsWith(open))
        {
            return 0;
        }

        var rest = slice[open.Length..];
        var endRel = rest.IndexOf(close, StringComparison.Ordinal);
        return endRel < 0 ? 0 : open.Length + endRel + close.Length;
    }

    /// <summary>Matches a run from the cursor up to (but not including) the first character in <paramref name="stopSet"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="stopSet">Characters that terminate the run.</param>
    /// <returns>Length matched on success; <c>0</c> when the cursor is on a stop character or the slice is empty.</returns>
    public static int MatchRunUntilAny(ReadOnlySpan<char> slice, SearchValues<char> stopSet)
    {
        ArgumentNullException.ThrowIfNull(stopSet);
        var stop = slice.IndexOfAny(stopSet);
        return stop switch
        {
            < 0 when slice is [] => 0,
            < 0 => slice.Length,
            0 => 0,
            _ => stop,
        };
    }

    /// <summary>Matches a single-quoted string where the embedded-quote escape is the doubled quote (<c>''</c>) rather than a backslash.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchSingleQuotedDoubledEscape(ReadOnlySpan<char> slice) =>
        MatchQuotedDoubledEscape(slice, '\'');

    /// <summary>Matches a double-quoted string where the embedded-quote escape is the doubled quote (<c>""</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (including the quotes), or <c>0</c>.</returns>
    public static int MatchDoubleQuotedDoubledEscape(ReadOnlySpan<char> slice) =>
        MatchQuotedDoubledEscape(slice, '"');

    /// <summary>
    /// Matches a bracketed block — required <paramref name="open"/> char, a body of any chars except
    /// <paramref name="close"/>, then a required <paramref name="close"/> char.
    /// Equivalent to <c>\G{open}[^{close}]*{close}</c>.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="open">Required opening character.</param>
    /// <param name="close">Required closing character; the body excludes this character.</param>
    /// <returns>Length matched (including both delimiters), or <c>0</c>.</returns>
    public static int MatchBracketedBlock(ReadOnlySpan<char> slice, char open, char close)
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
    /// adjacent to additional quote characters. Equivalent to C# 11's
    /// <c>"""..."""</c> shape.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="quote">Quote character (typically <c>'"'</c>).</param>
    /// <param name="minQuotes">Minimum opening / closing quote run (typically 3 — one or two are caught by other matchers).</param>
    /// <returns>Length matched (including both delimiter runs), or <c>0</c>.</returns>
    public static int MatchRawQuotedString(ReadOnlySpan<char> slice, char quote, int minQuotes)
    {
        const int OpenAndClosePairs = 2;
        ArgumentOutOfRangeException.ThrowIfLessThan(minQuotes, 1);
        if (slice.Length < minQuotes * OpenAndClosePairs)
        {
            return 0;
        }

        // Count the opening quote run.
        var openLen = 0;
        while (openLen < slice.Length && slice[openLen] == quote)
        {
            openLen++;
        }

        if (openLen < minQuotes)
        {
            return 0;
        }

        // Walk the body looking for a closing run of exactly openLen quotes
        // that isn't followed by another quote (which would belong to a longer
        // imagined closer) and isn't immediately preceded by a quote that
        // started this run earlier.
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
                // The closer is the last `openLen` quotes of the run; any
                // earlier quotes belong to the body. Cursor is already past
                // the run, so the match length is just the cursor position.
                return pos;
            }
        }

        return 0;
    }

    /// <summary>Matches a line terminator — <c>\r\n</c>, a bare <c>\r</c>, or a bare <c>\n</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int MatchNewline(ReadOnlySpan<char> slice)
    {
        const int CrLfLength = 2;
        return slice switch
        {
            ['\r', '\n', ..] => CrLfLength,
            ['\r', ..] or ['\n', ..] => 1,
            _ => 0,
        };
    }

    /// <summary>Matches a double-quoted string with backslash escapes followed by optional whitespace and a <c>:</c> lookahead — the property-key shape used by JSON and YAML.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the string literal on match (the colon is left for the punctuation rule).</returns>
    public static int MatchDoubleQuotedKey(ReadOnlySpan<char> slice)
    {
        var stringLen = MatchDoubleQuotedWithBackslashEscape(slice);
        if (stringLen is 0)
        {
            return 0;
        }

        var ws = MatchAsciiWhitespace(slice[stringLen..]);
        return stringLen + ws < slice.Length && slice[stringLen + ws] is ':' ? stringLen : 0;
    }

    /// <summary>Returns the length of the current line measured from <paramref name="slice"/>'s start (excluding the terminator).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length up to the next <c>\r</c> or <c>\n</c>, or <paramref name="slice"/>'s full length when no terminator is present.</returns>
    public static int LineLength(ReadOnlySpan<char> slice)
    {
        var nl = slice.IndexOfAny(LineTerminators);
        return nl < 0 ? slice.Length : nl;
    }

    /// <summary>Returns the length of the run starting at the cursor where every character is a member of <paramref name="set"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="set">Allowed characters.</param>
    /// <returns>Length of the run, or <c>0</c> when the cursor character isn't in <paramref name="set"/>.</returns>
    public static int MatchRunOf(ReadOnlySpan<char> slice, SearchValues<char> set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var stop = slice.IndexOfAnyExcept(set);
        return stop switch
        {
            < 0 when slice is [] => 0,
            < 0 => slice.Length,
            0 => 0,
            _ => stop,
        };
    }

    /// <summary>Consumes an optional <c>e</c>/<c>E</c> exponent (with optional sign and required digits) at the start of <paramref name="slice"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length consumed, or <c>0</c> when no valid exponent is present.</returns>
    private static int ConsumeExponent(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is not ('e' or 'E'))
        {
            return 0;
        }

        var pos = 1;
        if (pos < slice.Length && slice[pos] is '+' or '-')
        {
            pos++;
        }

        var digits = MatchRunOf(slice[pos..], AsciiDigits);
        return digits is 0 ? 0 : pos + digits;
    }
}
