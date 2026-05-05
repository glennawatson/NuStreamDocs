// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Java lexer.</summary>
/// <remarks>
/// Brace-style language with character literals, no preprocessor, and the
/// Java 15 text-block <c>"""..."""</c> form folded into the special-string rule.
/// </remarks>
public static class JavaLexer
{
    /// <summary>Minimum opening / closing quote run for a text-block literal.</summary>
    private const int TextBlockMinQuotes = 3;

    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "else"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "switch"u8],
        [.. "case"u8],
        [.. "default"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "return"u8],
        [.. "throw"u8],
        [.. "throws"u8],
        [.. "try"u8],
        [.. "catch"u8],
        [.. "finally"u8],
        [.. "new"u8],
        [.. "this"u8],
        [.. "super"u8],
        [.. "instanceof"u8],
        [.. "import"u8],
        [.. "package"u8],
        [.. "synchronized"u8],
        [.. "yield"u8],
        [.. "assert"u8]);

    /// <summary>Built-in primitive type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "boolean"u8],
        [.. "byte"u8],
        [.. "char"u8],
        [.. "short"u8],
        [.. "int"u8],
        [.. "long"u8],
        [.. "float"u8],
        [.. "double"u8],
        [.. "void"u8],
        [.. "var"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "class"u8],
        [.. "interface"u8],
        [.. "enum"u8],
        [.. "record"u8],
        [.. "extends"u8],
        [.. "implements"u8],
        [.. "permits"u8],
        [.. "sealed"u8],
        [.. "abstract"u8],
        [.. "final"u8],
        [.. "static"u8],
        [.. "public"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "transient"u8],
        [.. "volatile"u8],
        [.. "native"u8],
        [.. "strictfp"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. ">>>="u8],
        [.. "<<="u8],
        [.. ">>="u8],
        [.. ">>>"u8],
        [.. "->"u8],
        [.. "::"u8],
        [.. "++"u8],
        [.. "--"u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "<<"u8],
        [.. ">>"u8],
        [.. "+="u8],
        [.. "-="u8],
        [.. "*="u8],
        [.. "/="u8],
        [.. "%="u8],
        [.. "&="u8],
        [.. "|="u8],
        [.. "^="u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "&"u8],
        [.. "|"u8],
        [.. "^"u8],
        [.. "!"u8],
        [.. "~"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "?"u8]
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdefinprstwy"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("bcdfilsv"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acefilnprstv"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%=<>!&|^~?:"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@"u8);

    /// <summary>Integer-literal suffix bytes.</summary>
    private static readonly SearchValues<byte> IntegerSuffixSet = SearchValues.Create("lL"u8);

    /// <summary>Float-literal suffix bytes.</summary>
    private static readonly SearchValues<byte> FloatSuffixSet = SearchValues.Create("fFdD"u8);

    /// <summary>Gets the singleton Java lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Java lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        var textBlock = new LexerRule(
            static slice => TokenMatchers.MatchRawQuotedString(slice, (byte)'"', TextBlockMinQuotes),
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
            SpecialString = textBlock
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
