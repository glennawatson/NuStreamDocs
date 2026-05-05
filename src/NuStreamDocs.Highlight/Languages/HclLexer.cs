// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>HashiCorp Configuration Language (HCL) / Terraform lexer.</summary>
/// <remarks>
/// Block-style configuration: <c>resource "foo" "bar" { … }</c>. Recognizes
/// <c>#</c> and <c>//</c> line comments, <c>/* */</c> block comments, the
/// resource-block declaration keywords (<c>resource</c>, <c>variable</c>,
/// <c>data</c>, <c>module</c>, <c>output</c>, <c>locals</c>, <c>provider</c>,
/// <c>terraform</c>), and the standard HCL operators. <c>${...}</c>
/// interpolation expressions stay inside the surrounding string token.
/// </remarks>
public static class HclLexer
{
    /// <summary>General-keyword set (<c>for</c>, <c>in</c>, …).</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "for"u8],
        [.. "in"u8],
        [.. "if"u8],
        [.. "else"u8],
        [.. "endfor"u8],
        [.. "endif"u8]);

    /// <summary>Built-in primitive type keywords (Terraform 0.12+ type constraints).</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "string"u8],
        [.. "number"u8],
        [.. "bool"u8],
        [.. "any"u8],
        [.. "list"u8],
        [.. "map"u8],
        [.. "set"u8],
        [.. "object"u8],
        [.. "tuple"u8]);

    /// <summary>Block-declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "resource"u8],
        [.. "data"u8],
        [.. "variable"u8],
        [.. "output"u8],
        [.. "locals"u8],
        [.. "module"u8],
        [.. "provider"u8],
        [.. "terraform"u8],
        [.. "backend"u8],
        [.. "required_providers"u8],
        [.. "required_version"u8],
        [.. "dynamic"u8],
        [.. "lifecycle"u8],
        [.. "depends_on"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8]);

    /// <summary>First-byte set for whitespace runs.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for hash-prefixed comments.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("efi"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("abnlmsot"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("rdvolmptbl"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("=!<>&|+-*/%?:"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:"u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "=="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "=>"u8],
        [.. "->"u8],
        [.. "..."u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "!"u8],
        [.. "?"u8]
    ];

    /// <summary>Gets the singleton HCL lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the HCL lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },
            new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },
            new(LanguageCommon.LineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst },
            new(LanguageCommon.BlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst },
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },
            new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordTypes), TokenClass.KeywordType, LexerRule.NoStateChange) { FirstBytes = KeywordTypeFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordDeclarations), TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },
            new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },
            new(static slice => TokenMatchers.MatchLongestLiteral(slice, OperatorTable), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }
}
