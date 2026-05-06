// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Dart lexer.</summary>
/// <remarks>
/// Brace-style language without character literals (Dart has no <c>'x'</c> char form;
/// single quotes are strings) and without preprocessor directives. <c>${expr}</c>
/// interpolation is folded into the surrounding string token.
/// </remarks>
public static class DartLexer
{
    /// <summary>Minimum opening / closing quote run for a triple-quoted raw string.</summary>
    private const int TripleQuoteLength = 3;

    /// <summary>General-keyword set — shared C-family control-flow plus Dart-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "new this super is as in yield async await import library part show hide deferred rethrow assert"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "bool int double num String void dynamic Object Never Null Function Future Stream List Map Set Iterable"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "var final const late static abstract external factory covariant operator get set class interface mixin enum typedef extends implements with on sealed base"u8);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral);

    /// <summary>Operator alternation — shared C-style core plus Dart's null-safe / cascade / spread forms, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "??= ?? ?. ... .."u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Single-byte structural punctuation — shared C-curly set plus the Dart <c>@</c> annotation marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@"u8);

    /// <summary>Gets the singleton Dart lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Dart lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        var tripleQuoted = new LexerRule(
            static slice => TokenMatchers.MatchRawQuotedString(slice, (byte)'"', TripleQuoteLength),
            TokenClass.StringDouble,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst };

        CFamilyConfig config = new()
        {
            Keywords = Keywords,
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = true,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = false,
            WhitespaceIncludesNewlines = true,
            SpecialString = tripleQuoted
        };

        return CFamilyRules.CreateLexer(config);
    }
}
