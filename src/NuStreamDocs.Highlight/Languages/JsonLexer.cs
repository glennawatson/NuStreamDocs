// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;

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
    /// <summary>First-char set for string-shaped tokens (keys + values).</summary>
    private static readonly SearchValues<char> QuoteFirst = SearchValues.Create("\"");

    /// <summary>First-char set for numeric tokens (digits + leading minus).</summary>
    private static readonly SearchValues<char> NumberFirst = SearchValues.Create("-0123456789");

    /// <summary>First-char set for the <c>true</c> / <c>false</c> / <c>null</c> keyword constants.</summary>
    private static readonly SearchValues<char> KeywordFirst = SearchValues.Create("tfn");

    /// <summary>First-char set for structural punctuation.</summary>
    private static readonly SearchValues<char> PunctuationFirst = SearchValues.Create("{}[],:");

    /// <summary>Set of recognised JSON keyword constants.</summary>
    private static readonly FrozenSet<string> KeywordConstants = FrozenSet.ToFrozenSet(
        ["true", "false", "null"],
        StringComparer.Ordinal);

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        "json",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] = [

                // [ \t\r\n]+ whitespace runs.
                new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, NextState: null) { FirstChars = TokenMatchers.AsciiWhitespaceWithNewlines },

                // "..." string followed by ":" — property key. Must precede the plain string rule.
                new(TokenMatchers.MatchDoubleQuotedKey, TokenClass.NameAttribute, NextState: null) { FirstChars = QuoteFirst },

                // "..." string value with backslash escapes.
                new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, NextState: null) { FirstChars = QuoteFirst },

                // -?\d+\.\d+([eE][+-]?\d+)? float literal — must precede the integer rule.
                new(TokenMatchers.MatchSignedAsciiFloat, TokenClass.NumberFloat, NextState: null) { FirstChars = NumberFirst },

                // -?\d+ integer literal.
                new(TokenMatchers.MatchSignedAsciiInteger, TokenClass.NumberInteger, NextState: null) { FirstChars = NumberFirst },

                // true / false / null keyword constant.
                new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, NextState: null) { FirstChars = KeywordFirst },

                // Single-character structural punctuation: { } [ ] , :
                new(static slice => TokenMatchers.MatchSingleCharOf(slice, PunctuationFirst), TokenClass.Punctuation, NextState: null) { FirstChars = PunctuationFirst },
            ],
        }.ToFrozenDictionary(StringComparer.Ordinal));
}
