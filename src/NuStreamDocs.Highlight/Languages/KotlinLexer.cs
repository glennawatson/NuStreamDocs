// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Kotlin lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the
/// triple-quoted raw-string form folded into the special-string rule.
/// </remarks>
public static class KotlinLexer
{
    /// <summary>Minimum opening / closing quote run for a triple-quoted raw string.</summary>
    private const int RawStringMinQuotes = 3;

    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "else"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "when"u8],
        [.. "is"u8],
        [.. "as"u8],
        [.. "in"u8],
        [.. "out"u8],
        [.. "by"u8],
        [.. "where"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "return"u8],
        [.. "throw"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "yield"u8],
        [.. "this"u8],
        [.. "super"u8],
        [.. "import"u8],
        [.. "package"u8]);

    /// <summary>Built-in nominal type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "Boolean"u8],
        [.. "Byte"u8],
        [.. "Char"u8],
        [.. "Short"u8],
        [.. "Int"u8],
        [.. "Long"u8],
        [.. "Float"u8],
        [.. "Double"u8],
        [.. "String"u8],
        [.. "Unit"u8],
        [.. "Any"u8],
        [.. "Nothing"u8],
        [.. "Array"u8],
        [.. "List"u8],
        [.. "Map"u8],
        [.. "Set"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "val"u8],
        [.. "var"u8],
        [.. "fun"u8],
        [.. "class"u8],
        [.. "interface"u8],
        [.. "object"u8],
        [.. "enum"u8],
        [.. "data"u8],
        [.. "sealed"u8],
        [.. "open"u8],
        [.. "abstract"u8],
        [.. "final"u8],
        [.. "override"u8],
        [.. "lateinit"u8],
        [.. "const"u8],
        [.. "inline"u8],
        [.. "infix"u8],
        [.. "tailrec"u8],
        [.. "operator"u8],
        [.. "suspend"u8],
        [.. "vararg"u8],
        [.. "noinline"u8],
        [.. "crossinline"u8],
        [.. "reified"u8],
        [.. "internal"u8],
        [.. "public"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "companion"u8],
        [.. "annotation"u8],
        [.. "typealias"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "?:"u8],
        [.. "..="u8],
        [.. ".."u8],
        [.. "->"u8],
        [.. "::"u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "++"u8],
        [.. "--"u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "%="u8],
        [.. "?."u8],
        [.. "!!"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "!"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "?"u8]
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdefimoprstwy"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("ABCDFILMNSU"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acdefilnopstvr"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|?:."u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@"u8);

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
            KeywordFirst = KeywordFirst,
            KeywordTypes = KeywordTypes,
            KeywordTypeFirst = KeywordTypeFirst,
            KeywordDeclarations = KeywordDeclarations,
            KeywordDeclarationFirst = KeywordDeclarationFirst,
            KeywordConstants = KeywordConstants,
            KeywordConstantFirst = KeywordConstantFirst,
            Operators = OperatorTable,
            OperatorFirst = OperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = IntegerSuffixSet,
            FloatSuffix = FloatSuffixSet,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = rawString
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
