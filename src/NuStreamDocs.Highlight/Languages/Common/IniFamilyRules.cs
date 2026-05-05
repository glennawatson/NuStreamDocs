// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Reusable section / key / value lexer rule builder for the INI / TOML / properties / .editorconfig family.</summary>
/// <remarks>
/// Generalizes the line-anchored shape every flat-config dialect shares — section headers,
/// hash-or-semicolon comments, key-equals-value pairs — and lets each consumer toggle the
/// extras (string / numeric / boolean recognition, double-bracket headers, alternate
/// separators) via <see cref="IniFamilyConfig"/>.
/// </remarks>
internal static class IniFamilyRules
{
    /// <summary>First-byte set for whitespace runs (with newlines).</summary>
    public static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the section-header rule (<c>[</c>).</summary>
    public static readonly SearchValues<byte> BracketFirst = SearchValues.Create("["u8);

    /// <summary>First-byte set for double-quoted strings.</summary>
    public static readonly SearchValues<byte> DoubleQuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for single-quoted strings.</summary>
    public static readonly SearchValues<byte> SingleQuoteFirst = SearchValues.Create("'"u8);

    /// <summary>Identifier-continuation set for INI / TOML keys: letters, digits, underscore, dot, dash.</summary>
    public static readonly SearchValues<byte> KeyContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.-"u8);

    /// <summary>Builds a single-state INI-family <see cref="Lexer"/> from <paramref name="config"/> in one call.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Built lexer.</returns>
    public static Lexer CreateLexer(in IniFamilyConfig config) =>
        new(LanguageRuleBuilder.BuildSingleState(Build(config)));

    /// <summary>Builds the canonical INI-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in IniFamilyConfig config)
    {
        const int MaxRuleSlots = 12;
        var rules = new List<LexerRule>(MaxRuleSlots)
        {
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst }
        };

        // Comment to end-of-line. Configured first-byte set selects which prefixes are valid.
        var commentFirst = config.CommentFirst;
        rules.Add(new(slice => MatchCommentByPrefix(slice, commentFirst), TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = commentFirst });

        // [[double bracket]] — TOML-only, must precede the single-bracket rule.
        if (config.RecognizeDoubleBracketHeader)
        {
            rules.Add(new(MatchDoubleBracketHeader, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = BracketFirst, RequiresLineStart = true });
        }

        // [section] header.
        rules.Add(new(MatchBracketHeader, TokenClass.NameClass, LexerRule.NoStateChange) { FirstBytes = BracketFirst, RequiresLineStart = true });

        // Key followed by separator — emit key as NameAttribute; the separator + value follow as their own tokens.
        var separatorFirst = config.SeparatorFirst;
        rules.Add(new(
            slice => MatchKeyBeforeSeparator(slice, separatorFirst),
            TokenClass.NameAttribute,
            LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart, RequiresLineStart = true });

        // Separator byte (=, :).
        rules.Add(new(slice => TokenMatchers.MatchSingleByteOf(slice, separatorFirst), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = separatorFirst });

        if (config.RecognizeStringLiterals)
        {
            rules.Add(new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DoubleQuoteFirst });
            rules.Add(new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''), TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = SingleQuoteFirst });
        }

        if (config.KeywordConstants is { } constants && config.KeywordConstantFirst is { } constantFirst)
        {
            rules.Add(new(slice => TokenMatchers.MatchKeyword(slice, constants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = constantFirst });
        }

        if (config.RecognizeNumericLiterals)
        {
            rules.Add(new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });
            rules.Add(new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });
        }

        // Bare identifier on the value side (TOML enums, INI references) — falls through after constants.
        rules.Add(new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart });

        return [.. rules];
    }

    /// <summary>Matches a line comment whose introducer byte is in <paramref name="prefixSet"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefixSet">Allowed comment-introducer bytes.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchCommentByPrefix(ReadOnlySpan<byte> slice, SearchValues<byte> prefixSet)
    {
        if (slice is [] || !prefixSet.Contains(slice[0]))
        {
            return 0;
        }

        return TokenMatchers.LineLength(slice);
    }

    /// <summary>Matches a single-bracket section header — <c>[name]</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBracketHeader(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchBracketedBlock(slice, (byte)'[', (byte)']');

    /// <summary>Matches a TOML double-bracket array-of-tables header — <c>[[name]]</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchDoubleBracketHeader(ReadOnlySpan<byte> slice)
    {
        const int CloseBracketLength = 2;
        if (slice is not [(byte)'[', (byte)'[', ..])
        {
            return 0;
        }

        var close = slice.IndexOf("]]"u8);
        return close < 0 ? 0 : close + CloseBracketLength;
    }

    /// <summary>Matches a key (identifier-continue bytes) followed by optional whitespace and a byte from <paramref name="separatorSet"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="separatorSet">Allowed separator bytes (<c>=</c>, <c>:</c>).</param>
    /// <returns>Length of the key on a positive lookahead match; zero otherwise.</returns>
    private static int MatchKeyBeforeSeparator(ReadOnlySpan<byte> slice, SearchValues<byte> separatorSet)
    {
        if (slice is [] || !TokenMatchers.AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var keyEnd = slice[1..].IndexOfAnyExcept(KeyContinue);
        var keyLen = keyEnd < 0 ? slice.Length : 1 + keyEnd;
        var ws = TokenMatchers.MatchAsciiInlineWhitespace(slice[keyLen..]);
        var sepAt = keyLen + ws;
        return sepAt < slice.Length && separatorSet.Contains(slice[sepAt]) ? keyLen : 0;
    }
}
