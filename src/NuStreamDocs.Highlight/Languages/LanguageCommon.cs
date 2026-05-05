// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Cross-language byte-span matchers and <see cref="SearchValues{T}"/>
/// sets shared by the bundled lexers.
/// </summary>
internal static class LanguageCommon
{
    /// <summary>Length of the <c>///</c> doc-comment prefix.</summary>
    public const int DocCommentPrefixLength = 3;

    /// <summary>Length of a basic single-character literal: <c>'x'</c> = 3 bytes.</summary>
    public const int BasicCharLiteralLength = 3;

    /// <summary>Length of an escape-sequence single-character literal: <c>'\n'</c> = 4 bytes.</summary>
    public const int EscapedCharLiteralLength = 4;

    /// <summary>First-byte set for a leading <c>/</c> (line / block / doc comments).</summary>
    public static readonly SearchValues<byte> SlashFirst = SearchValues.Create("/"u8);

    /// <summary>First-byte set for single-quoted character / string literals.</summary>
    public static readonly SearchValues<byte> SingleQuoteFirst = SearchValues.Create("'"u8);

    /// <summary>First-byte set for double-quoted string literals.</summary>
    public static readonly SearchValues<byte> DoubleQuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for whitespace runs that include line terminators.</summary>
    public static readonly SearchValues<byte> WhitespaceWithNewlinesFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for hexadecimal numeric literals.</summary>
    public static readonly SearchValues<byte> HexFirst = SearchValues.Create("0"u8);

    /// <summary>First-byte set for decimal numeric literals (digits only).</summary>
    public static readonly SearchValues<byte> DigitFirst = TokenMatchers.AsciiDigits;

    /// <summary>First-byte set for integer literals (digits + leading underscore).</summary>
    public static readonly SearchValues<byte> IntegerFirst = SearchValues.Create("0123456789_"u8);

    /// <summary>First-byte set for C-curly structural punctuation.</summary>
    public static readonly SearchValues<byte> CCurlyPunctuationFirst = SearchValues.Create("(){}[];,.:"u8);

    /// <summary>First-byte set for an opening angle bracket (XML / Razor tag start).</summary>
    public static readonly SearchValues<byte> AngleOpenFirst = SearchValues.Create("<"u8);

    /// <summary>First-byte set for a closing angle bracket.</summary>
    public static readonly SearchValues<byte> AngleCloseFirst = SearchValues.Create(">"u8);

    /// <summary>First-byte set for the equals sign in attribute syntax.</summary>
    public static readonly SearchValues<byte> EqualsFirst = SearchValues.Create("="u8);

    /// <summary>First-byte set for SGML / XML entity references.</summary>
    public static readonly SearchValues<byte> EntityFirst = SearchValues.Create("&"u8);

    /// <summary>First-byte set for the at-sign Razor / verbatim-string trigger.</summary>
    public static readonly SearchValues<byte> AtFirst = SearchValues.Create("@"u8);

    /// <summary>First-byte set for an XML / Razor tag name (ASCII letters and underscore).</summary>
    public static readonly SearchValues<byte> TagNameFirst = TokenMatchers.AsciiIdentifierStart;

    /// <summary>First-byte set for an XML / Razor attribute name (ASCII letters, underscore, colon).</summary>
    public static readonly SearchValues<byte> AttributeNameFirst = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_:"u8);

    /// <summary>Length of a C-style block-comment opener (<c>/*</c>) and closer (<c>*/</c>).</summary>
    private const int BlockCommentDelimiterLength = 2;

    /// <summary>Length of a paired delimiter pair such as <c>""</c> wrapping a no-escape string.</summary>
    private const int PairedQuoteLength = 2;

    /// <summary>Length of a two-byte XML tag terminator such as <c>&lt;/</c> or <c>/&gt;</c>.</summary>
    private const int TwoCharTagDelimiter = 2;

    /// <summary>Continuation set for XML / Razor attribute and tag names — letters, digits, underscore, colon, dot, dash.</summary>
    private static readonly SearchValues<byte> XmlNameContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_:.-"u8);

    /// <summary>Allowed bytes inside an entity reference body (between <c>&amp;</c> and <c>;</c>).</summary>
    private static readonly SearchValues<byte> EntityBody = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789#"u8);

    /// <summary>C-style line comment — <c>//</c> to end of line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int LineComment(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchLineCommentToEol(slice, (byte)'/', (byte)'/');

    /// <summary><c>///</c> XML doc-comment to end of line — must precede the regular <c>//</c> line-comment matcher.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (zero when the slice doesn't open with <c>///</c>).</returns>
    public static int XmlDocCommentToEol(ReadOnlySpan<byte> slice) =>
        slice is [(byte)'/', (byte)'/', (byte)'/', ..]
            ? DocCommentPrefixLength + TokenMatchers.LineLength(slice[DocCommentPrefixLength..])
            : 0;

    /// <summary>Single-character literal: <c>'x'</c> or <c>'\x'</c> (matches the typical C / C# / F# shape).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>3 for a basic literal, 4 for the escape form, 0 otherwise.</returns>
    public static int CharLiteral(ReadOnlySpan<byte> slice) => slice switch
    {
        [(byte)'\'', (byte)'\\', _, (byte)'\'', ..] => EscapedCharLiteralLength,
        [(byte)'\'', _, (byte)'\'', ..] => BasicCharLiteralLength,
        _ => 0
    };

    /// <summary>C-style block comment — <c>/* ... */</c> non-greedy.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int BlockComment(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < 4 || slice[0] is not (byte)'/' || slice[1] is not (byte)'*')
        {
            return 0;
        }

        var rest = slice[BlockCommentDelimiterLength..];
        var close = rest.IndexOf("*/"u8);
        return close < 0 ? 0 : BlockCommentDelimiterLength + close + BlockCommentDelimiterLength;
    }

    /// <summary>Double-quoted no-escape string.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int DoubleQuotedStringNoEscape(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'"')
        {
            return 0;
        }

        var close = slice[1..].IndexOf((byte)'"');
        return close < 0 ? 0 : PairedQuoteLength + close;
    }

    /// <summary>Open-angle followed by a slash — XML closing tag start.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns><c>2</c> on match, <c>0</c> on miss.</returns>
    public static int AngleOpenSlash(ReadOnlySpan<byte> slice) =>
        slice.Length >= TwoCharTagDelimiter && slice[0] is (byte)'<' && slice[1] is (byte)'/' ? TwoCharTagDelimiter : 0;

    /// <summary>Self-closing tag terminator — <c>/&gt;</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns><c>2</c> on match, <c>0</c> on miss.</returns>
    public static int SelfClose(ReadOnlySpan<byte> slice) =>
        slice.Length >= TwoCharTagDelimiter && slice[0] is (byte)'/' && slice[1] is (byte)'>' ? TwoCharTagDelimiter : 0;

    /// <summary>SGML / XML entity reference — <c>&amp;name;</c> or <c>&amp;#1234;</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int EntityReference(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'&')
        {
            return 0;
        }

        var bodyStop = slice[1..].IndexOfAnyExcept(EntityBody);
        return bodyStop <= 0 || 1 + bodyStop >= slice.Length || slice[1 + bodyStop] is not (byte)';'
            ? 0
            : 1 + bodyStop + 1;
    }

    /// <summary>XML / Razor attribute name followed by <c>=</c> (lookahead, not consumed).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the attribute name on a positive match.</returns>
    public static int AttributeName(ReadOnlySpan<byte> slice)
    {
        var nameLen = TokenMatchers.MatchIdentifier(slice, AttributeNameFirst, XmlNameContinue);
        if (nameLen is 0)
        {
            return 0;
        }

        var ws = TokenMatchers.MatchAsciiWhitespace(slice[nameLen..]);
        return nameLen + ws < slice.Length && slice[nameLen + ws] is (byte)'=' ? nameLen : 0;
    }

    /// <summary>Verbatim string <c>@"..."</c> with <c>""</c> as the embedded-quote escape.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int MatchVerbatimString(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'@')
        {
            return 0;
        }

        var body = TokenMatchers.MatchDoubleQuotedDoubledEscape(slice[1..]);
        return body is 0 ? 0 : 1 + body;
    }

    /// <summary>Preprocessor directive — optional leading <c>[ \t]</c>, then <c>#</c>, then the rest of the line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int MatchHashPreprocessor(ReadOnlySpan<byte> slice)
    {
        var indent = TokenMatchers.MatchAsciiInlineWhitespace(slice);
        return indent >= slice.Length || slice[indent] is not (byte)'#'
            ? 0
            : indent + 1 + TokenMatchers.LineLength(slice[(indent + 1)..]);
    }

    /// <summary>Unsigned float literal with an optional one-byte suffix from <paramref name="suffix"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="suffix">Recognized trailing suffix bytes.</param>
    /// <returns>Length matched.</returns>
    public static int MatchFloatWithOptionalSuffix(ReadOnlySpan<byte> slice, SearchValues<byte> suffix)
    {
        var matched = TokenMatchers.MatchUnsignedAsciiFloat(slice);
        if (matched is 0)
        {
            return 0;
        }

        if (matched < slice.Length && suffix.Contains(slice[matched]))
        {
            return matched + 1;
        }

        return matched;
    }

    /// <summary>XML / Razor tag name.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    public static int TagName(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchIdentifier(slice, TagNameFirst, XmlNameContinue);
}
