// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>GraphQL schema and query lexer.</summary>
/// <remarks>
/// Schema-shape language with <c>#</c> line comments, <c>type</c> / <c>scalar</c>
/// / <c>enum</c> / <c>interface</c> / <c>union</c> declarations, <c>$variable</c>
/// references, and the <c>!</c> non-null marker.
/// </remarks>
public static class GraphQLLexer
{
    /// <summary>Minimum opening / closing quote run for a triple-quoted block-string description.</summary>
    private const int TripleQuoteLength = 3;

    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "query"u8],
        [.. "mutation"u8],
        [.. "subscription"u8],
        [.. "fragment"u8],
        [.. "on"u8],
        [.. "schema"u8],
        [.. "directive"u8],
        [.. "extend"u8],
        [.. "implements"u8],
        [.. "repeatable"u8]);

    /// <summary>Built-in scalar type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "Int"u8],
        [.. "Float"u8],
        [.. "String"u8],
        [.. "Boolean"u8],
        [.. "ID"u8]);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "type"u8],
        [.. "scalar"u8],
        [.. "enum"u8],
        [.. "interface"u8],
        [.. "union"u8],
        [.. "input"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8]);

    /// <summary>First-byte set for whitespace runs.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for hash-prefixed comments.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for double-quoted strings (and triple-quoted descriptions).</summary>
    private static readonly SearchValues<byte> DoubleQuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for the variable / directive sigil rule.</summary>
    private static readonly SearchValues<byte> SigilFirst = SearchValues.Create("$@"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("dfimoqrs"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("BFISI"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("teisuiu"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("=!|&"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[],:"u8);

    /// <summary>Gets the singleton GraphQL lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the GraphQL lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },
            new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },

            // """description""" must precede the regular string rule.
            new(static slice => TokenMatchers.MatchRawQuotedString(slice, (byte)'"', TripleQuoteLength), TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DoubleQuoteFirst },

            // "..." string with backslash escapes.
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DoubleQuoteFirst },

            // $var or @directive sigil.
            new(MatchSigilName, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = SigilFirst },

            new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordTypes), TokenClass.KeywordType, LexerRule.NoStateChange) { FirstBytes = KeywordTypeFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordDeclarations), TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },

            new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, OperatorFirst), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches a <c>$variable</c> or <c>@directive</c> reference — sigil + identifier body.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchSigilName(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || (slice[0] is not (byte)'$' && slice[0] is not (byte)'@'))
        {
            return 0;
        }

        var ident = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        return ident is 0 ? 0 : 1 + ident;
    }
}
