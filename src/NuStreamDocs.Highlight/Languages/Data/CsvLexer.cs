// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Data;

/// <summary>Comma-separated-values lexer (RFC 4180 shape).</summary>
/// <remarks>
/// Three token classes: <see cref="TokenClass.Punctuation"/> for the
/// <c>,</c> separator, <see cref="TokenClass.StringDouble"/> for
/// quoted fields (with the doubled-quote <c>""</c> embedded-quote
/// escape), and <see cref="TokenClass.Name"/> for unquoted field
/// content. Newlines pass through as <see cref="TokenClass.Whitespace"/>.
/// </remarks>
public static class CsvLexer
{
    /// <summary>First-byte set for the <c>,</c> field separator.</summary>
    private static readonly SearchValues<byte> CommaFirst = SearchValues.Create(","u8);

    /// <summary>First-byte set for double-quoted field content.</summary>
    private static readonly SearchValues<byte> QuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for line terminators.</summary>
    private static readonly SearchValues<byte> NewlineFirst = SearchValues.Create("\r\n"u8);

    /// <summary>Bytes that terminate an unquoted field — separator, line terminator, or an opening quote.</summary>
    private static readonly SearchValues<byte> UnquotedFieldStop = SearchValues.Create(",\r\n\""u8);

    /// <summary>Gets the singleton CSV lexer.</summary>
    public static Lexer Instance { get; } = new(LanguageRuleBuilder.BuildSingleState(
    [
        new(
            static slice => TokenMatchers.MatchSingleByteOf(slice, CommaFirst),
            TokenClass.Punctuation,
            LexerRule.NoStateChange) { FirstBytes = CommaFirst },
        new(
            TokenMatchers.MatchDoubleQuotedDoubledEscape,
            TokenClass.StringDouble,
            LexerRule.NoStateChange) { FirstBytes = QuoteFirst },
        new(
            TokenMatchers.MatchNewline,
            TokenClass.Whitespace,
            LexerRule.NoStateChange) { FirstBytes = NewlineFirst },
        new(
            MatchUnquotedField,
            TokenClass.Name,
            LexerRule.NoStateChange)
    ]));

    /// <summary>Matches an unquoted CSV field — bytes up to the next separator, line terminator, or quote.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (at least one byte), or zero when the cursor is already on a delimiter.</returns>
    private static int MatchUnquotedField(ReadOnlySpan<byte> slice)
    {
        var stop = slice.IndexOfAny(UnquotedFieldStop);
        if (stop is 0)
        {
            return 0;
        }

        return stop < 0 ? slice.Length : stop;
    }
}
