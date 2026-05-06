// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Data;

/// <summary>JSON lexer.</summary>
/// <remarks>
/// Strings, numbers, the literal keywords <c>true</c> / <c>false</c> /
/// <c>null</c>, and structural punctuation. Property keys (strings
/// followed by <c>:</c>) classify as <see cref="TokenClass.NameAttribute"/>
/// (CSS class <c>na</c>) which existing themes style as a distinct colour
/// from value strings.
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
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create([.. "true"u8], [.. "false"u8], [.. "null"u8]);

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        SpecialString = new(TokenMatchers.MatchDoubleQuotedKey, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = QuoteFirst },
        IncludeDoubleQuotedString = true,
        IncludeSignedFloatLiteral = true,
        IncludeSignedIntegerLiteral = true,
        NumberFirst = NumberFirst,
        KeywordConstants = KeywordConstants,
        KeywordConstantFirst = KeywordFirst,
        Punctuation = PunctuationFirst
    });
}
