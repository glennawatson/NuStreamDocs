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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. CFamilyShared.ControlFlow,
        [.. "throws"u8],
        [.. "new"u8],
        [.. "this"u8],
        [.. "super"u8],
        [.. "instanceof"u8],
        [.. "import"u8],
        [.. "package"u8],
        [.. "as"u8],
        [.. "in"u8],
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
        [.. "void"u8]);

    /// <summary>Declaration / modifier keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "def"u8],
        [.. "class"u8],
        [.. "interface"u8],
        [.. "trait"u8],
        [.. "enum"u8],
        [.. "extends"u8],
        [.. "implements"u8],
        [.. "abstract"u8],
        [.. "final"u8],
        [.. "static"u8],
        [.. "public"u8],
        [.. "private"u8],
        [.. "protected"u8],
        [.. "synchronized"u8],
        [.. "transient"u8],
        [.. "volatile"u8]);

    /// <summary>Constant keywords — shared <c>true</c> / <c>false</c> / <c>null</c>.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(CFamilyShared.TrueFalseNull);

    /// <summary>Operator alternation — shared C-style core plus Groovy's unsigned right-shift / null-safe / spread forms.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. ">>>="u8],
        [.. ">>>"u8],
        [.. "::"u8],
        [.. "?:"u8],
        [.. "?."u8],
        [.. "*."u8],
        .. CFamilyShared.StandardOperators
    ];

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abcdefinprstw"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("bcdfilsv"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("acdefilnprstv"u8);

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
            IntegerSuffix = SearchValues.Create("lLgG"u8),
            FloatSuffix = SearchValues.Create("fFdDgG"u8),
            IncludeDocComment = false,
            IncludePreprocessor = false,
            IncludeCharacterLiteral = true,
            WhitespaceIncludesNewlines = true,
            SpecialString = tripleQuoted
        };

        return new(LanguageRuleBuilder.BuildSingleState(CFamilyRules.Build(config)));
    }
}
