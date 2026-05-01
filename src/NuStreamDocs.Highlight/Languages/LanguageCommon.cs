// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Cross-language matchers and <see cref="SearchValues{T}"/> sets shared
/// by the bundled lexers.
/// </summary>
internal static class LanguageCommon
{
    /// <summary>First-char set for a leading <c>/</c> (line / block / doc comments).</summary>
    public static readonly SearchValues<char> SlashFirst = SearchValues.Create("/");

    /// <summary>First-char set for single-quoted character / string literals.</summary>
    public static readonly SearchValues<char> SingleQuoteFirst = SearchValues.Create("'");

    /// <summary>First-char set for double-quoted string literals.</summary>
    public static readonly SearchValues<char> DoubleQuoteFirst = SearchValues.Create("\"");

    /// <summary>First-char set for whitespace runs that include line terminators.</summary>
    public static readonly SearchValues<char> WhitespaceWithNewlinesFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-char set for hexadecimal numeric literals.</summary>
    public static readonly SearchValues<char> HexFirst = SearchValues.Create("0");

    /// <summary>First-char set for decimal numeric literals (digits only).</summary>
    public static readonly SearchValues<char> DigitFirst = TokenMatchers.AsciiDigits;

    /// <summary>First-char set for integer literals (digits + leading underscore).</summary>
    public static readonly SearchValues<char> IntegerFirst = SearchValues.Create("0123456789_");

    /// <summary>First-char set for C-curly structural punctuation.</summary>
    public static readonly SearchValues<char> CCurlyPunctuationFirst = SearchValues.Create("(){}[];,.:");

    /// <summary>First-char set for an opening angle bracket (XML / Razor tag start).</summary>
    public static readonly SearchValues<char> AngleOpenFirst = SearchValues.Create("<");

    /// <summary>First-char set for a closing angle bracket.</summary>
    public static readonly SearchValues<char> AngleCloseFirst = SearchValues.Create(">");

    /// <summary>First-char set for the equals sign in attribute syntax.</summary>
    public static readonly SearchValues<char> EqualsFirst = SearchValues.Create("=");

    /// <summary>First-char set for SGML / XML entity references.</summary>
    public static readonly SearchValues<char> EntityFirst = SearchValues.Create("&");

    /// <summary>First-char set for the at-sign Razor / verbatim-string trigger.</summary>
    public static readonly SearchValues<char> AtFirst = SearchValues.Create("@");

    /// <summary>First-char set for an XML / Razor tag name (ASCII letters and underscore).</summary>
    public static readonly SearchValues<char> TagNameFirst = TokenMatchers.AsciiIdentifierStart;

    /// <summary>First-char set for an XML / Razor attribute name (ASCII letters, underscore, colon).</summary>
    public static readonly SearchValues<char> AttributeNameFirst = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_:");

    /// <summary>Length of a C-style block-comment opener (<c>/*</c>) and closer (<c>*/</c>).</summary>
    private const int BlockCommentDelimiterLength = 2;

    /// <summary>Length of a paired delimiter pair such as <c>""</c> wrapping a no-escape string.</summary>
    private const int PairedQuoteLength = 2;

    /// <summary>Length of a two-character XML tag terminator such as <c>&lt;/</c> or <c>/&gt;</c>.</summary>
    private const int TwoCharTagDelimiter = 2;

    /// <summary>Continuation set for XML / Razor attribute and tag names — letters, digits, underscore, colon, dot, dash.</summary>
    private static readonly SearchValues<char> XmlNameContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_:.-");

    /// <summary>Allowed characters inside an entity reference body (between <c>&amp;</c> and <c>;</c>).</summary>
    private static readonly SearchValues<char> EntityBody = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789#");

    /// <summary>C-style line comment — <c>//</c> to end of line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int LineComment(ReadOnlySpan<char> slice) =>
        TokenMatchers.MatchLineCommentToEol(slice, '/', '/');

    /// <summary>C-style block comment — <c>/* ... */</c> non-greedy.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int BlockComment(ReadOnlySpan<char> slice)
    {
        if (slice.Length < 4 || slice[0] is not '/' || slice[1] is not '*')
        {
            return 0;
        }

        var rest = slice[BlockCommentDelimiterLength..];
        var close = rest.IndexOf("*/", StringComparison.Ordinal);
        return close < 0 ? 0 : BlockCommentDelimiterLength + close + BlockCommentDelimiterLength;
    }

    /// <summary>Double-quoted no-escape string.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int DoubleQuotedStringNoEscape(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is not '"')
        {
            return 0;
        }

        var close = slice[1..].IndexOf('"');
        return close < 0 ? 0 : PairedQuoteLength + close;
    }

    /// <summary>Open-angle followed by a slash — XML closing tag start.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns><c>2</c> on match, <c>0</c> on miss.</returns>
    public static int AngleOpenSlash(ReadOnlySpan<char> slice) =>
        slice.Length >= TwoCharTagDelimiter && slice[0] is '<' && slice[1] is '/' ? TwoCharTagDelimiter : 0;

    /// <summary>Self-closing tag terminator — <c>/&gt;</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns><c>2</c> on match, <c>0</c> on miss.</returns>
    public static int SelfClose(ReadOnlySpan<char> slice) =>
        slice.Length >= TwoCharTagDelimiter && slice[0] is '/' && slice[1] is '>' ? TwoCharTagDelimiter : 0;

    /// <summary>SGML / XML entity reference — <c>&amp;name;</c> or <c>&amp;#1234;</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int EntityReference(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is not '&')
        {
            return 0;
        }

        var bodyStop = slice[1..].IndexOfAnyExcept(EntityBody);
        if (bodyStop is <= 0 || 1 + bodyStop >= slice.Length || slice[1 + bodyStop] is not ';')
        {
            return 0;
        }

        return 1 + bodyStop + 1;
    }

    /// <summary>XML / Razor attribute name followed by <c>=</c> (lookahead, not consumed).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the attribute name on a positive match.</returns>
    public static int AttributeName(ReadOnlySpan<char> slice)
    {
        var nameLen = TokenMatchers.MatchIdentifier(slice, AttributeNameFirst, XmlNameContinue);
        if (nameLen is 0)
        {
            return 0;
        }

        var ws = TokenMatchers.MatchAsciiWhitespace(slice[nameLen..]);
        return nameLen + ws < slice.Length && slice[nameLen + ws] is '=' ? nameLen : 0;
    }

    /// <summary>XML / Razor tag name.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int TagName(ReadOnlySpan<char> slice) =>
        TokenMatchers.MatchIdentifier(slice, TagNameFirst, XmlNameContinue);
}
