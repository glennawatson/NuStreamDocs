// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Groovy lexer (covers Gradle build files via the <c>gradle</c> alias).</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and triple-quoted raw
/// strings (matching Java's text-block shape) folded into the special-string rule.
/// </remarks>
public static class GroovyLexer
{
    /// <summary>Minimum opening / closing quote run for a triple-quoted string.</summary>
    private const int TripleQuoteLength = 3;

    /// <summary>General-keyword set — shared C-family control-flow plus Groovy-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "throws new this super instanceof import package as in assert"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "boolean byte char short int long float double void"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "def class interface trait enum extends implements abstract final static public private protected synchronized transient volatile"u8);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral);

    /// <summary>Operator alternation — shared C-style core plus Groovy's unsigned right-shift / null-safe / spread forms, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        ">>>= >>> :: ?: ?. *."u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Single-byte structural punctuation — shared C-curly set plus the Groovy <c>@</c> annotation marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@"u8);

    /// <summary>Gets the singleton Groovy lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Groovy lexer.</summary>
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
            IntegerSuffix = SearchValues.Create("lLgG"u8),
            FloatSuffix = SearchValues.Create("fFdDgG"u8),
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = tripleQuoted
        };

        return CFamilyRules.CreateLexer(config);
    }
}
