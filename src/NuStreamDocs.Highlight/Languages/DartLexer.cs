// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "new"u8],
        [.. "this"u8],
        [.. "super"u8],
        [.. "is"u8],
        [.. "as"u8],
        [.. "in"u8],
        [.. "yield"u8],
        [.. "async"u8],
        [.. "await"u8],
        [.. "import"u8],
        [.. "library"u8],
        [.. "part"u8],
        [.. "show"u8],
        [.. "hide"u8],
        [.. "deferred"u8],
        [.. "rethrow"u8],
        [.. "assert"u8]]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "bool"u8],
        [.. "int"u8],
        [.. "double"u8],
        [.. "num"u8],
        [.. "String"u8],
        [.. "void"u8],
        [.. "dynamic"u8],
        [.. "Object"u8],
        [.. "Never"u8],
        [.. "Null"u8],
        [.. "Function"u8],
        [.. "Future"u8],
        [.. "Stream"u8],
        [.. "List"u8],
        [.. "Map"u8],
        [.. "Set"u8],
        [.. "Iterable"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "var"u8],
        [.. "final"u8],
        [.. "const"u8],
        [.. "late"u8],
        [.. "static"u8],
        [.. "abstract"u8],
        [.. "external"u8],
        [.. "factory"u8],
        [.. "covariant"u8],
        [.. "operator"u8],
        [.. "get"u8],
        [.. "set"u8],
        [.. "class"u8],
        [.. "interface"u8],
        [.. "mixin"u8],
        [.. "enum"u8],
        [.. "typedef"u8],
        [.. "extends"u8],
        [.. "implements"u8],
        [.. "with"u8],
        [.. "on"u8],
        [.. "sealed"u8],
        [.. "base"u8]);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(CFamilyShared.TrueFalseNull);

    /// <summary>Operator alternation — shared C-style core plus Dart's null-safe / cascade / spread forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "??="u8],
        [.. "??"u8],
        [.. "?."u8],
        [.. "..."u8],
        [.. ".."u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdefhilnprstwyx"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("bdfiSnvNOMLF"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("abcdefginlmoptsvw"u8);

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
            IntegerSuffix = CFamilyRules.NoSuffix,
            FloatSuffix = CFamilyRules.NoSuffix,
            IncludeDocComment = true,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = false,
            WhitespaceIncludesNewlines = true,
            SpecialString = tripleQuoted
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
