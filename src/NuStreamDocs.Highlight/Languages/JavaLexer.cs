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

    /// <summary>General-keyword set — shared C-family control-flow plus Java-specific extras.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "throws"u8],
        [.. "new"u8],
        [.. "this"u8],
        [.. "super"u8],
        [.. "instanceof"u8],
        [.. "import"u8],
        [.. "package"u8],
        [.. "synchronized"u8],
        [.. "yield"u8],
        [.. "assert"u8]]);

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

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(CFamilyShared.TrueFalseNull);

    /// <summary>Operator alternation — shared C-style core plus Java's unsigned right-shift forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. ">>>="u8],
        [.. ">>>"u8],
        [.. "::"u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>Single-byte structural punctuation — shared C-curly set plus the Java <c>@</c> annotation marker.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.@"u8);

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
            KeywordTypes = KeywordTypes,
            KeywordDeclarations = KeywordDeclarations,
            KeywordConstants = KeywordConstants,
            Operators = OperatorTable,
            OperatorFirst = CFamilyShared.StandardOperatorFirst,
            Punctuation = PunctuationSet,
            IntegerSuffix = CFamilyShared.JvmIntegerSuffix,
            FloatSuffix = CFamilyShared.JvmFloatSuffix,
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = textBlock
        };

        return CFamilyRules.CreateLexer(config);
    }
}
