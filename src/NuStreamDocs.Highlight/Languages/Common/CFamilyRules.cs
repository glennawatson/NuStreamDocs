// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Reusable C-family lexer rule builder.</summary>
/// <remarks>
/// Generalizes the rule shape shared by every brace-delimited, slash-comment
/// language (C, C++, Java, Kotlin, Swift, Go, Rust, Scala, Dart, Objective-C).
/// Differences across the family — preprocessor on/off, raw-string flavor,
/// character literal on/off, sigil prefixes, integer / float suffix sets — are
/// captured by <see cref="CFamilyConfig"/> so a new lexer is one keyword
/// table away from compiling.
/// </remarks>
internal static class CFamilyRules
{
    /// <summary>Default integer-body bytes — digits plus underscore separator.</summary>
    public static readonly SearchValues<byte> DigitsWithUnderscore = SearchValues.Create("0123456789_"u8);

    /// <summary>Hex-body bytes — hex digits plus underscore separator.</summary>
    public static readonly SearchValues<byte> HexBodyWithUnderscore = SearchValues.Create("0123456789abcdefABCDEF_"u8);

    /// <summary>Empty suffix set — used by languages that have no integer / float type suffixes.</summary>
    public static readonly SearchValues<byte> NoSuffix = SearchValues.Create(ReadOnlySpan<byte>.Empty);

    /// <summary>First-byte set for inline whitespace runs (no newlines).</summary>
    public static readonly SearchValues<byte> InlineWhitespaceFirst = SearchValues.Create(" \t"u8);

    /// <summary>First-byte set for whitespace runs that include line terminators.</summary>
    public static readonly SearchValues<byte> WhitespaceWithNewlinesFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>Builds a single-state C-family <see cref="Lexer"/> from <paramref name="config"/> in one call.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Built lexer.</returns>
    public static Lexer CreateLexer(in CFamilyConfig config) =>
        new(LanguageRuleBuilder.BuildSingleState(Build(config)));

    /// <summary>Builds the canonical C-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in CFamilyConfig config)
    {
        var rules = new CStyleRuleSet(
            Whitespace: BuildWhitespaceRule(config),
            DocComment: config.IncludeDocComment ? BuildDocCommentRule() : null,
            LineComment: BuildLineCommentRule(),
            BlockComment: BuildBlockCommentRule(),
            Preprocessor: config.IncludePreprocessor ? BuildPreprocessorRule() : null,
            SpecialString: config.SpecialString,
            DoubleString: BuildDoubleStringRule(),
            SingleString: BuildSingleStringRule(config.IncludeCharacterLiteral),
            CharacterLiteral: config.IncludeCharacterLiteral ? BuildCharLiteralRule() : null,
            HexNumber: BuildHexNumberRule(config),
            FloatNumber: BuildFloatNumberRule(config),
            IntegerNumber: BuildIntegerNumberRule(config),
            KeywordConstant: BuildKeywordRule(config.KeywordConstants, config.KeywordConstantFirst, TokenClass.KeywordConstant),
            KeywordType: BuildKeywordRule(config.KeywordTypes, config.KeywordTypeFirst, TokenClass.KeywordType),
            KeywordDeclaration: BuildKeywordRule(config.KeywordDeclarations, config.KeywordDeclarationFirst, TokenClass.KeywordDeclaration),
            Keyword: BuildKeywordRule(config.Keywords, config.KeywordFirst, TokenClass.Keyword),
            Identifier: BuildIdentifierRule(config),
            Operator: BuildOperatorRule(config),
            Punctuation: BuildPunctuationRule(config));

        return LanguageRuleBuilder.BuildCStyleRules(rules);
    }

    /// <summary>Whitespace rule — newlines included by default (most C-family languages don't anchor to lines).</summary>
    /// <param name="config">Configuration.</param>
    /// <returns>The whitespace rule.</returns>
    private static LexerRule BuildWhitespaceRule(in CFamilyConfig config) =>
        config.WhitespaceIncludesNewlines
            ? new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceWithNewlinesFirst }
            : new(TokenMatchers.MatchAsciiInlineWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = InlineWhitespaceFirst };

    /// <summary>Doc-comment rule — <c>///</c> to end-of-line.</summary>
    /// <returns>The doc-comment rule.</returns>
    private static LexerRule BuildDocCommentRule() =>
        new(LanguageCommon.XmlDocCommentToEol, TokenClass.CommentSpecial, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst };

    /// <summary>Line-comment rule — <c>//</c> to end-of-line.</summary>
    /// <returns>The line-comment rule.</returns>
    private static LexerRule BuildLineCommentRule() =>
        new(LanguageCommon.LineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst };

    /// <summary>Block-comment rule — non-greedy <c>/* ... */</c>.</summary>
    /// <returns>The block-comment rule.</returns>
    private static LexerRule BuildBlockCommentRule() =>
        new(LanguageCommon.BlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst };

    /// <summary>Preprocessor rule — line-anchored <c>#</c> directive.</summary>
    /// <returns>The preprocessor rule.</returns>
    private static LexerRule BuildPreprocessorRule() =>
        new(LanguageCommon.MatchHashPreprocessor, TokenClass.CommentPreproc, LexerRule.NoStateChange)
        {
            FirstBytes = SearchValues.Create(" \t#"u8),
            RequiresLineStart = true
        };

    /// <summary>Double-quoted string with backslash escapes.</summary>
    /// <returns>The double-string rule.</returns>
    private static LexerRule BuildDoubleStringRule() =>
        new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst };

    /// <summary>Single-quoted string rule.</summary>
    /// <param name="includeCharacterLiteral">Whether the language has dedicated character literals.</param>
    /// <returns>The single-string rule — never-match no-op when char literals consume the single-quote, otherwise a backslash-escape rule.</returns>
    private static LexerRule BuildSingleStringRule(bool includeCharacterLiteral)
    {
        // When the language has character literals (C / C++ / Rust / Java),
        // single-quote is consumed by the char rule, so put a no-op rule that
        // never fires here. Languages without char literals can still surface
        // a single-quoted string form via SpecialString.
        if (includeCharacterLiteral)
        {
            return new(static _ => 0, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst };
        }

        return new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''), TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst };
    }

    /// <summary>Single-character literal rule — <c>'x'</c> or <c>'\x'</c>.</summary>
    /// <returns>The character-literal rule.</returns>
    private static LexerRule BuildCharLiteralRule() =>
        new(LanguageCommon.CharLiteral, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst };

    /// <summary>Hex-literal rule — <c>0x...</c> with optional suffix.</summary>
    /// <param name="config">Configuration.</param>
    /// <returns>The hex-literal rule.</returns>
    private static LexerRule BuildHexNumberRule(in CFamilyConfig config)
    {
        var suffix = config.IntegerSuffix;
        return new(
            slice => TokenMatchers.MatchAsciiHexLiteral(slice, HexBodyWithUnderscore, suffix),
            TokenClass.NumberHex,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.HexFirst };
    }

    /// <summary>Float-literal rule — <c>1.0[eE...][suffix]</c>.</summary>
    /// <param name="config">Configuration.</param>
    /// <returns>The float-literal rule.</returns>
    private static LexerRule BuildFloatNumberRule(in CFamilyConfig config)
    {
        var suffix = config.FloatSuffix;
        return new(slice => LanguageCommon.MatchFloatWithOptionalSuffix(slice, suffix), TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DigitFirst };
    }

    /// <summary>Integer-literal rule — digits with optional suffix.</summary>
    /// <param name="config">Configuration.</param>
    /// <returns>The integer-literal rule.</returns>
    private static LexerRule BuildIntegerNumberRule(in CFamilyConfig config)
    {
        var suffix = config.IntegerSuffix;
        return new(
            slice => TokenMatchers.MatchRunWithSuffix(slice, LanguageCommon.IntegerFirst, suffix),
            TokenClass.NumberInteger,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.IntegerFirst };
    }

    /// <summary>Keyword-set rule. Falls back to <see cref="ByteKeywordSet.FirstByteSet"/> when <paramref name="firstBytes"/> is null.</summary>
    /// <param name="keywords">Keyword set.</param>
    /// <param name="firstBytes">Optional first-byte dispatch override; null falls back to the auto-derived set.</param>
    /// <param name="tokenClass">Classification to emit.</param>
    /// <returns>The keyword rule.</returns>
    private static LexerRule BuildKeywordRule(ByteKeywordSet keywords, SearchValues<byte>? firstBytes, TokenClass tokenClass) =>
        new(slice => TokenMatchers.MatchKeyword(slice, keywords), tokenClass, LexerRule.NoStateChange) { FirstBytes = firstBytes ?? keywords.FirstByteSet };

    /// <summary>Identifier rule; uses the language-specific identifier-start / continue sets when supplied, else the ASCII-letter default.</summary>
    /// <param name="config">Configuration.</param>
    /// <returns>The identifier rule.</returns>
    private static LexerRule BuildIdentifierRule(in CFamilyConfig config)
    {
        if (config.IdentifierFirst is { } first && config.IdentifierContinue is { } cont)
        {
            return new(slice => TokenMatchers.MatchIdentifier(slice, first, cont), TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = first };
        }

        return new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart };
    }

    /// <summary>Operator-alternation rule (longest-first).</summary>
    /// <param name="config">Configuration.</param>
    /// <returns>The operator rule.</returns>
    private static LexerRule BuildOperatorRule(in CFamilyConfig config)
    {
        var operators = config.Operators;
        return new(slice => TokenMatchers.MatchLongestLiteral(slice, operators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = config.OperatorFirst };
    }

    /// <summary>Single-byte punctuation rule.</summary>
    /// <param name="config">Configuration.</param>
    /// <returns>The punctuation rule.</returns>
    private static LexerRule BuildPunctuationRule(in CFamilyConfig config)
    {
        var punct = config.Punctuation;
        return new(slice => TokenMatchers.MatchSingleByteOf(slice, punct), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = punct };
    }
}
