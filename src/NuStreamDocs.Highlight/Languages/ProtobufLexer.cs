// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Protocol Buffers (proto2 / proto3) schema lexer.</summary>
/// <remarks>
/// Schema-shape: <c>//</c> and <c>/* */</c> comments, <c>message</c> /
/// <c>enum</c> / <c>service</c> / <c>rpc</c> declarations, plus the standard
/// scalar-type keyword set.
/// </remarks>
public static class ProtobufLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "syntax"u8],
        [.. "package"u8],
        [.. "import"u8],
        [.. "option"u8],
        [.. "returns"u8],
        [.. "stream"u8],
        [.. "reserved"u8],
        [.. "extensions"u8],
        [.. "to"u8]);

    /// <summary>Built-in scalar type keywords.</summary>
    private static readonly ByteKeywordSet KeywordTypes = ByteKeywordSet.Create(
        [.. "double"u8],
        [.. "float"u8],
        [.. "int32"u8],
        [.. "int64"u8],
        [.. "uint32"u8],
        [.. "uint64"u8],
        [.. "sint32"u8],
        [.. "sint64"u8],
        [.. "fixed32"u8],
        [.. "fixed64"u8],
        [.. "sfixed32"u8],
        [.. "sfixed64"u8],
        [.. "bool"u8],
        [.. "string"u8],
        [.. "bytes"u8]);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "message"u8],
        [.. "enum"u8],
        [.. "service"u8],
        [.. "rpc"u8],
        [.. "extend"u8],
        [.. "oneof"u8],
        [.. "map"u8],
        [.. "repeated"u8],
        [.. "optional"u8],
        [.. "required"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8]);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("eipoorst"u8);

    /// <summary>First-byte set for type keywords.</summary>
    private static readonly SearchValues<byte> KeywordTypeFirst = SearchValues.Create("bdfisu"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("emorsop"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tf"u8);

    /// <summary>First-byte set for whitespace runs.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.<>"u8);

    /// <summary>First-byte set for the equals-sign operator (<c>field = 1</c>).</summary>
    private static readonly SearchValues<byte> EqualsFirst = SearchValues.Create("="u8);

    /// <summary>Gets the singleton Protobuf lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Protobuf lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },
            new(LanguageCommon.LineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst },
            new(LanguageCommon.BlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst },
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },
            new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''), TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },
            new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordTypes), TokenClass.KeywordType, LexerRule.NoStateChange) { FirstBytes = KeywordTypeFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordDeclarations), TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },
            new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, EqualsFirst), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = EqualsFirst },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }
}
