// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>Kotlin lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the
/// triple-quoted raw-string form folded into the special-string rule.
/// </remarks>
public static class KotlinLexer
{
    /// <summary>Minimum opening / closing quote run for a triple-quoted raw string.</summary>
    private const int RawStringMinQuotes = 3;

    /// <summary>General-keyword set — shared C-family control-flow plus Kotlin-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "when is as in out by where yield this super import package"u8);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "Boolean Byte Char Short Int Long Float Double String Unit Any Nothing Array List Map Set"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "val var fun class interface object enum data sealed open abstract final override lateinit const inline infix tailrec operator suspend vararg noinline crossinline reified"u8,
        "internal public private protected companion annotation typealias"u8);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "?: ..= .. -> :: == != <= >= && || ++ -- += -= *= /= %= ?. !! + - * / % ! = < > ?"u8);

    /// <summary>Integer-literal suffix bytes.</summary>
    private static readonly SearchValues<byte> IntegerSuffixSet = SearchValues.Create("Llu"u8);

    /// <summary>Float-literal suffix bytes.</summary>
    private static readonly SearchValues<byte> FloatSuffixSet = SearchValues.Create("fF"u8);

    /// <summary>Gets the singleton Kotlin lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Kotlin lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        var rawString = new LexerRule(
            static slice => TokenMatchers.MatchRawQuotedString(slice, (byte)'"', RawStringMinQuotes),
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
            Punctuation = CFamilyShared.AnnotationPunctuation,
            IntegerSuffix = IntegerSuffixSet,
            FloatSuffix = FloatSuffixSet,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = rawString
        };

        return CFamilyRules.CreateLexer(config);
    }
}
