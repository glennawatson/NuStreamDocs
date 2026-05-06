// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Reusable CSS / SCSS / Less rule builder.</summary>
/// <remarks>
/// Single-state lexer covering selectors, properties, values, at-rules,
/// hex colour literals, and dimensioned numbers. The configuration toggles
/// the SCSS / Less extensions: <c>//</c> line comments, <c>$</c> / <c>@</c>
/// variable sigils, and the <c>&amp;</c> parent-reference selector.
/// </remarks>
internal static class CssFamilyRules
{
    /// <summary>First-byte set for whitespace runs (with newlines).</summary>
    public static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for hex colour literals (<c>#</c>) and ID selectors.</summary>
    public static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for at-rules (<c>@import</c>, <c>@media</c>, …).</summary>
    public static readonly SearchValues<byte> AtFirst = SearchValues.Create("@"u8);

    /// <summary>First-byte set for class selectors (<c>.</c>).</summary>
    public static readonly SearchValues<byte> DotFirst = SearchValues.Create("."u8);

    /// <summary>First-byte set for double-quoted strings.</summary>
    public static readonly SearchValues<byte> DoubleQuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for single-quoted strings.</summary>
    public static readonly SearchValues<byte> SingleQuoteFirst = SearchValues.Create("'"u8);

    /// <summary>First-byte set for the <c>!important</c> annotation.</summary>
    public static readonly SearchValues<byte> BangFirst = SearchValues.Create("!"u8);

    /// <summary>First-byte set for the parent-reference selector (<c>&amp;</c>).</summary>
    public static readonly SearchValues<byte> AmpFirst = SearchValues.Create("&"u8);

    /// <summary>Identifier-continuation set: letters, digits, underscore, dash.</summary>
    public static readonly SearchValues<byte> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-"u8);

    /// <summary>Hex digits (no underscore — CSS hex colours are pure hex).</summary>
    public static readonly SearchValues<byte> HexDigits = SearchValues.Create("0123456789abcdefABCDEF"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    public static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,:"u8);

    /// <summary>First-byte set for combinator / arithmetic operators.</summary>
    public static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+>~*/=|^$"u8);

    /// <summary>Maximum hex-colour digit count after <c>#</c> (<c>#rrggbbaa</c> is the widest form).</summary>
    private const int MaxHexColorDigitCount = 8;

    /// <summary>Three-digit hex colour (<c>#rgb</c>).</summary>
    private const int ShortHexColorDigitCount = 3;

    /// <summary>Four-digit hex colour (<c>#rgba</c>).</summary>
    private const int ShortAlphaHexColorDigitCount = 4;

    /// <summary>Six-digit hex colour (<c>#rrggbb</c>).</summary>
    private const int LongHexColorDigitCount = 6;

    /// <summary>Eight-digit hex colour (<c>#rrggbbaa</c>).</summary>
    private const int LongAlphaHexColorDigitCount = 8;

    /// <summary>Length of the literal text <c>important</c>.</summary>
    private const int ImportantWordLength = 9;

    /// <summary>Builds a single-state CSS-family <see cref="Lexer"/> from <paramref name="config"/> in one call.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Built lexer.</returns>
    public static Lexer CreateLexer(in CssFamilyConfig config) =>
        new(LanguageRuleBuilder.BuildSingleState(Build(config)));

    /// <summary>Builds the CSS-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in CssFamilyConfig config)
    {
        const int MaxRuleSlots = 16;
        var rules = new List<LexerRule>(MaxRuleSlots)
        {
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },
            new(LanguageCommon.BlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst }
        };

        if (config.IncludeLineComment)
        {
            rules.Add(new(LanguageCommon.LineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst });
        }

        rules.Add(new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DoubleQuoteFirst });
        rules.Add(new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''), TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = SingleQuoteFirst });

        // Hex colour literal #abc / #abcdef must precede the ID-selector rule.
        rules.Add(new(MatchHexColor, TokenClass.NumberHex, LexerRule.NoStateChange) { FirstBytes = HashFirst });

        // ID selector #foo (any identifier-continue bytes after the leading #).
        rules.Add(new(MatchIdSelector, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = HashFirst });

        // Class selector .foo
        rules.Add(new(MatchClassSelector, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = DotFirst });

        rules.Add(new(MatchAtIdentifier, TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = AtFirst });

        // SCSS variable $var.
        if (config.VariableSigil is (byte)'$')
        {
            rules.Add(new(MatchDollarVariable, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = SearchValues.Create("$"u8) });
        }

        // Parent reference & — SCSS / Less only.
        if (config.IncludeParentSelector)
        {
            rules.Add(new(MatchParentSelector, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = AmpFirst });
        }

        // !important annotation.
        rules.Add(new(MatchImportant, TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = BangFirst });

        // Dimensioned float (1.5em, 100%, 12.5px).
        rules.Add(new(MatchFloatWithUnit, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });

        // Dimensioned integer (12px, 100%).
        rules.Add(new(MatchIntegerWithUnit, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });

        // Identifier (property name, value keyword, element selector). Continue set includes dash so
        // CSS properties like `font-size` and values like `box-sizing` classify as one token.
        rules.Add(new(MatchCssIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart });

        rules.Add(new(static slice => TokenMatchers.MatchSingleByteOf(slice, OperatorFirst), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst });
        rules.Add(new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet });

        return [.. rules];
    }

    /// <summary>Matches a CSS hex colour literal: <c>#</c> followed by exactly 3, 4, 6, or 8 hex digits.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchHexColor(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'#')
        {
            return 0;
        }

        var digitCount = CountHexDigits(slice[1..]);

        // Don't consume followed-by-identifier sequences like "#fff-not-color"; require the run to end at a non-identifier byte.
        var endPos = 1 + digitCount;
        if (endPos < slice.Length && IdentifierContinue.Contains(slice[endPos]))
        {
            return 0;
        }

        return IsValidHexColorDigitCount(digitCount) ? endPos : 0;
    }

    /// <summary>Counts a leading run of hex digits up to <see cref="MaxHexColorDigitCount"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor (after the leading <c>#</c>).</param>
    /// <returns>Hex-digit run length.</returns>
    private static int CountHexDigits(ReadOnlySpan<byte> slice)
    {
        var pos = 0;
        while (pos < slice.Length && pos < MaxHexColorDigitCount && HexDigits.Contains(slice[pos]))
        {
            pos++;
        }

        return pos;
    }

    /// <summary>Returns true when <paramref name="digitCount"/> matches one of the recognized CSS hex-colour widths.</summary>
    /// <param name="digitCount">Hex-digit count.</param>
    /// <returns>True for 3, 4, 6, or 8 digits.</returns>
    private static bool IsValidHexColorDigitCount(int digitCount) => digitCount is
        ShortHexColorDigitCount or
        ShortAlphaHexColorDigitCount or
        LongHexColorDigitCount or
        LongAlphaHexColorDigitCount;

    /// <summary>Matches an ID selector: <c>#</c> followed by an identifier body.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchIdSelector(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPrefixedRun(slice, (byte)'#', IdentifierContinue);

    /// <summary>Matches a class selector: <c>.</c> followed by an identifier body.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchClassSelector(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'.')
        {
            return 0;
        }

        // Reject .5 (decimal-leading float) — let the float rule handle that.
        if (slice.Length > 1 && TokenMatchers.AsciiDigits.Contains(slice[1]))
        {
            return 0;
        }

        return TokenMatchers.MatchPrefixedRun(slice, (byte)'.', IdentifierContinue);
    }

    /// <summary>Matches an at-rule or Less variable: <c>@</c> followed by an identifier body.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchAtIdentifier(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPrefixedRun(slice, (byte)'@', IdentifierContinue);

    /// <summary>Matches a SCSS dollar variable: <c>$</c> followed by an identifier body.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchDollarVariable(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPrefixedRun(slice, (byte)'$', IdentifierContinue);

    /// <summary>Matches a parent-reference selector — bare <c>&amp;</c> not followed by another <c>&amp;</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchParentSelector(ReadOnlySpan<byte> slice) =>
        slice is [(byte)'&', ..] && (slice.Length < 2 || slice[1] is not (byte)'&') ? 1 : 0;

    /// <summary>Matches the <c>!important</c> annotation, allowing inner whitespace.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchImportant(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'!')
        {
            return 0;
        }

        var pos = 1 + TokenMatchers.MatchAsciiInlineWhitespace(slice[1..]);
        return slice[pos..].StartsWith("important"u8) ? pos + ImportantWordLength : 0;
    }

    /// <summary>Matches a CSS float literal followed by an optional unit (<c>em</c>, <c>%</c>, …).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchFloatWithUnit(ReadOnlySpan<byte> slice)
    {
        var floatLen = TokenMatchers.MatchUnsignedAsciiFloat(slice);
        if (floatLen is 0)
        {
            return 0;
        }

        return floatLen + ConsumeUnit(slice[floatLen..]);
    }

    /// <summary>Matches a CSS integer literal followed by an optional unit.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchIntegerWithUnit(ReadOnlySpan<byte> slice)
    {
        var intLen = TokenMatchers.MatchAsciiDigits(slice);
        if (intLen is 0)
        {
            return 0;
        }

        return intLen + ConsumeUnit(slice[intLen..]);
    }

    /// <summary>Matches an optional unit suffix (identifier-continue bytes or <c>%</c>) following a numeric literal.</summary>
    /// <param name="slice">Slice anchored after the numeric body.</param>
    /// <returns>Length consumed.</returns>
    private static int ConsumeUnit(ReadOnlySpan<byte> slice)
    {
        if (slice is [(byte)'%', ..])
        {
            return 1;
        }

        if (slice is [] || !TokenMatchers.AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var stop = slice[1..].IndexOfAnyExcept(TokenMatchers.AsciiIdentifierContinue);
        return stop < 0 ? slice.Length : 1 + stop;
    }

    /// <summary>Matches a CSS identifier — leading letter, then letters / digits / underscore / dash.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchCssIdentifier(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchIdentifier(slice, TokenMatchers.AsciiIdentifierStart, IdentifierContinue);
}
