// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>JSON lexer.</summary>
/// <remarks>
/// Strings, numbers, the literal keywords <c>true</c> / <c>false</c> /
/// <c>null</c>, and structural punctuation. Pygments classifies
/// property keys (strings followed by <c>:</c>) under <c>Name.Tag</c>
/// (CSS class <c>nt</c>); we fold those into <see cref="TokenClass.NameAttribute"/>
/// (CSS class <c>na</c>) which existing themes also style.
/// </remarks>
public static class JsonLexer
{
    /// <summary>First-byte set for string-shaped tokens (keys + values).</summary>
    private static readonly SearchValues<byte> QuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for numeric tokens (digits + leading minus).</summary>
    private static readonly SearchValues<byte> NumberFirst = SearchValues.Create("-0123456789"u8);

    /// <summary>First-byte set for the <c>true</c> / <c>false</c> / <c>null</c> keyword constants.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationFirst = SearchValues.Create("{}[],:"u8);

    /// <summary>Set of recognized JSON keyword constants.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create("true", "false", "null");

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        LanguageRuleBuilder.BuildSingleState([

            // [ \t\r\n]+ whitespace runs.
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiWhitespaceWithNewlines },

            // "..." string followed by ":" — property key. Must precede the plain string rule.
            new(TokenMatchers.MatchDoubleQuotedKey, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = QuoteFirst },

            // "..." string value with backslash escapes.
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = QuoteFirst },

            // -?\d+\.\d+([eE][+-]?\d+)? float literal — must precede the integer rule.
            new(TokenMatchers.MatchSignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = NumberFirst },

            // -?\d+ integer literal.
            new(TokenMatchers.MatchSignedAsciiInteger, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = NumberFirst },

            // true / false / null keyword constant.
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },

            // Single-byte structural punctuation: { } [ ] , :
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationFirst), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationFirst },
        ]));
}
