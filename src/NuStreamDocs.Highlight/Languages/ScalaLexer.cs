// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Scala lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the triple-quoted
/// raw-string form folded into the special-string rule. Covers Scala 3 / 2 keywords.
/// </remarks>
public static class ScalaLexer
{
    /// <summary>Minimum opening / closing quote run for a triple-quoted raw string.</summary>
    private const int RawStringMinQuotes = 3;

    /// <summary>General-keyword set — shared C-family control-flow plus Scala-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "match"u8],
        [.. "yield"u8],
        [.. "new"u8],
        [.. "this"u8],
        [.. "super"u8],
        [.. "import"u8],
        [.. "package"u8],
        [.. "with"u8],
        [.. "extends"u8],
        [.. "derives"u8],
        [.. "as"u8],
        [.. "given"u8],
        [.. "using"u8],
        [.. "then"u8]]);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "Boolean"u8],
        [.. "Byte"u8],
        [.. "Short"u8],
        [.. "Int"u8],
        [.. "Long"u8],
        [.. "Float"u8],
        [.. "Double"u8],
        [.. "Char"u8],
        [.. "String"u8],
        [.. "Unit"u8],
        [.. "Any"u8],
        [.. "AnyVal"u8],
        [.. "AnyRef"u8],
        [.. "Nothing"u8],
        [.. "Null"u8],
        [.. "Option"u8],
        [.. "List"u8],
        [.. "Seq"u8],
        [.. "Map"u8],
        [.. "Set"u8],
        [.. "Array"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "val"u8],
        [.. "var"u8],
        [.. "def"u8],
        [.. "type"u8],
        [.. "class"u8],
        [.. "object"u8],
        [.. "trait"u8],
        [.. "enum"u8],
        [.. "abstract"u8],
        [.. "final"u8],
        [.. "sealed"u8],
        [.. "open"u8],
        [.. "implicit"u8],
        [.. "lazy"u8],
        [.. "override"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "public"u8],
        [.. "inline"u8],
        [.. "transparent"u8],
        [.. "opaque"u8],
        [.. "extension"u8]);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(CFamilyShared.TrueFalseNull);

    /// <summary>Operator alternation — shared C-style core plus Scala's <c>&lt;-</c> / <c>=&gt;</c> / <c>::</c> arrows.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "<-"u8],
        [.. "=>"u8],
        [.. "::"u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("acdefgimnprstuwy"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("ABCDFILMNOSU"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acdefilmnoprstvx"u8);

    /// <summary>Single-byte structural punctuation — shared C-curly set plus the Scala <c>@</c> annotation marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@"u8);

    /// <summary>Gets the singleton Scala lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Scala lexer.</summary>
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
            KeywordFirst = KeywordFirst,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypeFirst,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = CFamilyShared.TrueFalseNullFirst,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyShared.JvmIntegerSuffix,
            FloatSuffix = CFamilyShared.JvmFloatSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = rawString
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
