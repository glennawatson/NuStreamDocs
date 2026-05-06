// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Scripting;

namespace NuStreamDocs.Highlight.Languages.Common.Builders;

/// <summary>Generic single-state lexer rule-list builder for bespoke languages that don't fit a tighter family helper.</summary>
/// <remarks>
/// Consumed by <see cref="LuaLexer"/>, <see cref="NimLexer"/>, <see cref="JuliaLexer"/>,
/// <see cref="MatlabLexer"/>, <see cref="RLexer"/>, <see cref="ErlangLexer"/>, and similar — each
/// language declares its keyword sets, comment / string rules, and operator / punctuation tables
/// once in a <see cref="SingleStateLexerConfig"/>, then calls <see cref="CreateLexer"/> to get a
/// finished <see cref="Lexer"/>. The boilerplate <c>new(LanguageRuleBuilder.BuildSingleState(...))</c>
/// triple, every keyword-rule line, and the standard whitespace / number rules all live here once.
/// </remarks>
internal static class SingleStateLexerRules
{
    /// <summary>Builds a single-state <see cref="Lexer"/> from <paramref name="config"/> in one call.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Built lexer.</returns>
    public static Lexer CreateLexer(in SingleStateLexerConfig config) =>
        new(LanguageRuleBuilder.BuildSingleState(Build(config)));

    /// <summary>Builds the ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in SingleStateLexerConfig config)
    {
        const int MaxRuleSlots = 16;
        var rules = new List<LexerRule>(MaxRuleSlots)
        {
            new(
                TokenMatchers.MatchAsciiWhitespace,
                TokenClass.Whitespace,
                LexerRule.NoStateChange) { FirstBytes = config.WhitespaceFirst ?? TokenMatchers.AsciiWhitespaceWithNewlines }
        };

        AppendIfPresent(rules, config.PreCommentRule);
        AppendIfPresent(rules, config.LineComment);
        AppendIfPresent(rules, config.AlternateLineComment);
        AppendIfPresent(rules, config.BlockComment);
        AppendIfPresent(rules, config.SpecialString);

        if (config.IncludeDoubleQuotedString)
        {
            rules.Add(new(
                TokenMatchers.MatchDoubleQuotedWithBackslashEscape,
                TokenClass.StringDouble,
                LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst });
        }

        if (config.IncludeSingleQuotedString)
        {
            rules.Add(new(
                static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''),
                TokenClass.StringSingle,
                LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst });
        }

        AppendAll(rules, config.PostStringRules);

        var numberFirst = config.NumberFirst ?? TokenMatchers.AsciiDigits;
        if (config.IncludeSignedFloatLiteral)
        {
            rules.Add(new(
                TokenMatchers.MatchSignedAsciiFloat,
                TokenClass.NumberFloat,
                LexerRule.NoStateChange) { FirstBytes = numberFirst });
        }
        else if (config.IncludeFloatLiteral)
        {
            rules.Add(new(
                TokenMatchers.MatchUnsignedAsciiFloat,
                TokenClass.NumberFloat,
                LexerRule.NoStateChange) { FirstBytes = numberFirst });
        }

        if (config.IncludeSignedIntegerLiteral)
        {
            rules.Add(new(
                TokenMatchers.MatchSignedAsciiInteger,
                TokenClass.NumberInteger,
                LexerRule.NoStateChange) { FirstBytes = numberFirst });
        }
        else if (config.IncludeIntegerLiteral)
        {
            rules.Add(new(
                TokenMatchers.MatchAsciiDigits,
                TokenClass.NumberInteger,
                LexerRule.NoStateChange) { FirstBytes = numberFirst });
        }

        var lineStart = config.KeywordsRequireLineStart;
        AppendKeywordRule(rules, config.KeywordConstants, config.KeywordConstantFirst, TokenClass.KeywordConstant, lineStart);
        AppendKeywordRule(rules, config.KeywordTypes, config.KeywordTypeFirst, TokenClass.KeywordType, lineStart);
        AppendKeywordRule(rules, config.KeywordDeclarations, config.KeywordDeclarationFirst, TokenClass.KeywordDeclaration, lineStart);
        AppendKeywordRule(rules, config.Keywords, config.KeywordFirst, TokenClass.Keyword, lineStart);
        AppendAll(rules, config.ExtraRules);
        AppendKeywordRule(rules, config.BuiltinKeywords, config.BuiltinKeywordFirst, TokenClass.NameBuiltin, requiresLineStart: false);

        if (!config.SuppressIdentifierRule)
        {
            AppendIdentifierRule(rules, config.IdentifierContinue);
        }

        AppendOperatorRule(rules, config.Operators, config.OperatorFirst);
        AppendPunctuationRule(rules, config.Punctuation);

        return [.. rules];
    }

    /// <summary>Appends <paramref name="rule"/> to <paramref name="rules"/> when non-null.</summary>
    /// <param name="rules">Target rule list.</param>
    /// <param name="rule">Optional rule.</param>
    private static void AppendIfPresent(List<LexerRule> rules, LexerRule? rule)
    {
        if (rule is null)
        {
            return;
        }

        rules.Add(rule);
    }

    /// <summary>Appends every rule in <paramref name="extras"/> when non-null.</summary>
    /// <param name="rules">Target rule list.</param>
    /// <param name="extras">Optional rule array.</param>
    private static void AppendAll(List<LexerRule> rules, LexerRule[]? extras)
    {
        if (extras is null)
        {
            return;
        }

        for (var i = 0; i < extras.Length; i++)
        {
            rules.Add(extras[i]);
        }
    }

    /// <summary>Appends a keyword-set rule to <paramref name="rules"/> when both <paramref name="keywords"/> and <paramref name="firstBytes"/> are supplied.</summary>
    /// <param name="rules">Target rule list.</param>
    /// <param name="keywords">Keyword set.</param>
    /// <param name="firstBytes">First-byte dispatch set.</param>
    /// <param name="tokenClass">Classification.</param>
    /// <param name="requiresLineStart">When true, only fires at start-of-line positions.</param>
    private static void AppendKeywordRule(List<LexerRule> rules, ByteKeywordSet? keywords, SearchValues<byte>? firstBytes, TokenClass tokenClass, bool requiresLineStart)
    {
        if (keywords is null)
        {
            return;
        }

        var captured = keywords;
        rules.Add(new(
            slice => TokenMatchers.MatchKeyword(slice, captured),
            tokenClass,
            LexerRule.NoStateChange) { FirstBytes = firstBytes ?? captured.FirstByteSet, RequiresLineStart = requiresLineStart });
    }

    /// <summary>Appends the identifier rule to <paramref name="rules"/> — uses <paramref name="continueSet"/> when supplied, else the ASCII default.</summary>
    /// <param name="rules">Target rule list.</param>
    /// <param name="continueSet">Optional identifier-continuation set.</param>
    private static void AppendIdentifierRule(List<LexerRule> rules, SearchValues<byte>? continueSet)
    {
        if (continueSet is { } cont)
        {
            rules.Add(new(
                slice => TokenMatchers.MatchIdentifier(slice, TokenMatchers.AsciiIdentifierStart, cont),
                TokenClass.Name,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart });
            return;
        }

        rules.Add(new(
            TokenMatchers.MatchAsciiIdentifier,
            TokenClass.Name,
            LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart });
    }

    /// <summary>Appends the operator-alternation rule to <paramref name="rules"/> when both <paramref name="operators"/> and <paramref name="firstBytes"/> are supplied.</summary>
    /// <param name="rules">Target rule list.</param>
    /// <param name="operators">Operator alternation, longest-first.</param>
    /// <param name="firstBytes">First-byte dispatch set.</param>
    private static void AppendOperatorRule(List<LexerRule> rules, byte[][]? operators, SearchValues<byte>? firstBytes)
    {
        if (operators is null)
        {
            return;
        }

        var captured = operators;
        var dispatch = firstBytes ?? OperatorAlternationFactory.FirstBytesOf(operators);
        rules.Add(new(
            slice => TokenMatchers.MatchLongestLiteral(slice, captured),
            TokenClass.Operator,
            LexerRule.NoStateChange) { FirstBytes = dispatch });
    }

    /// <summary>Appends the single-byte structural-punctuation rule to <paramref name="rules"/> when <paramref name="punctuation"/> is supplied.</summary>
    /// <param name="rules">Target rule list.</param>
    /// <param name="punctuation">Structural punctuation byte set.</param>
    private static void AppendPunctuationRule(List<LexerRule> rules, SearchValues<byte>? punctuation)
    {
        if (punctuation is null)
        {
            return;
        }

        var captured = punctuation;
        rules.Add(new(
            slice => TokenMatchers.MatchSingleByteOf(slice, captured),
            TokenClass.Punctuation,
            LexerRule.NoStateChange) { FirstBytes = captured });
    }
}
