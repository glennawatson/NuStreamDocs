// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;
using NuStreamDocs.Highlight.Languages.Common.Families;

namespace NuStreamDocs.Highlight.Languages.CFamily;

/// <summary>V lexer.</summary>
/// <remarks>
/// Brace-style language modeled on Go's surface — <c>fn</c>, <c>struct</c>,
/// <c>module</c>, <c>pub</c>, <c>mut</c>, with character literals and no
/// preprocessor. <c>$</c>-interpolation in strings stays inside the string
/// token.
/// </remarks>
public static class VLexer
{
    /// <summary>General-keyword set — shared C-family control-flow plus V-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        CFamilyShared.ControlFlowLiteral,
        "as in is or match select go goto defer spawn lock rlock unsafe asm shared atomic $if $else $for"u8);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.CreateFromSpaceSeparated(
        "bool byte i8 i16 int i64 u8 u16 u32 u64 f32 f64 rune string voidptr size_t char any none"u8);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "fn struct interface enum type module import pub mut const static __global"u8);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.CreateFromSpaceSeparated(CFamilyShared.TrueFalseNullLiteral);

    /// <summary>Operator alternation — shared C-style core plus V's range / arrow forms, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        "... .. := <- ->"u8,
        CFamilyShared.StandardOperatorsLiteral);

    /// <summary>Single-byte structural punctuation — shared C-curly set plus V's <c>@</c> attribute marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:@"u8);

    /// <summary>Gets the singleton V lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the V lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
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
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = null
        };

        return CFamilyRules.CreateLexer(config);
    }
}
