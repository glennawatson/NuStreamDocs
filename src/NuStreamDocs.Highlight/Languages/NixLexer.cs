// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Nix expression-language lexer.</summary>
/// <remarks>
/// <c>#</c> line comments, <c>/* ... */</c> block comments, the
/// <c>let</c>/<c>in</c>/<c>with</c>/<c>rec</c>/<c>inherit</c> binding shape,
/// and the <c>${...}</c> string-interpolation form folded into the surrounding
/// string token. Path literals (<c>./foo</c>, <c>/abs/path</c>, <c>~/home</c>)
/// classify as a single name token.
/// </remarks>
public static class NixLexer
{
    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "then"u8],
        [.. "else"u8],
        [.. "with"u8],
        [.. "assert"u8],
        [.. "or"u8],
        [.. "import"u8]);

    /// <summary>Declaration / structure keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "let"u8],
        [.. "in"u8],
        [.. "rec"u8],
        [.. "inherit"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "null"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "//"u8],
        [.. "->"u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "&&"u8],
        [.. "||"u8],
        [.. "?"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "!"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8]
    ];

    /// <summary>First-byte set for whitespace.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the <c>#</c> line-comment rule.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for path literals (<c>.</c>, <c>/</c>, <c>~</c>).</summary>
    private static readonly SearchValues<byte> PathFirst = SearchValues.Create("./~"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("aeiotw"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("ilr"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/<>!=&|?"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,.:@"u8);

    /// <summary>Identifier-continuation set — Nix identifiers may contain <c>'</c>, <c>-</c>, and <c>_</c>.</summary>
    private static readonly SearchValues<byte> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-'"u8);

    /// <summary>Path-body byte set — letters, digits, dot, slash, dash, underscore.</summary>
    private static readonly SearchValues<byte> PathContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-./"u8);

    /// <summary>Gets the singleton Nix lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Nix lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },
            new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },
            new(LanguageCommon.BlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst },

            // Multi-line ''...'' string. Must precede the regular string rule.
            new(MatchIndentedString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },

            // "..." string with backslash escapes (interpolation folded inside).
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

            // Path literal (./foo, /abs, ~/home) — must precede the operator rule because `/` would otherwise be consumed as the update operator.
            new(MatchPathLiteral, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = PathFirst },

            new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordDeclarations), TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },

            new(
                static slice => TokenMatchers.MatchIdentifier(slice, TokenMatchers.AsciiIdentifierStart, IdentifierContinue),
                TokenClass.Name,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },
            new(static slice => TokenMatchers.MatchLongestLiteral(slice, OperatorTable), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches a Nix indented string <c>'' ... ''</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchIndentedString(ReadOnlySpan<byte> slice)
    {
        const int OpenLength = 2;
        const int CloseLength = 2;
        if (slice.Length < OpenLength + CloseLength || slice[0] is not (byte)'\'' || slice[1] is not (byte)'\'')
        {
            return 0;
        }

        var rest = slice[OpenLength..];
        var close = rest.IndexOf("''"u8);
        return close < 0 ? 0 : OpenLength + close + CloseLength;
    }

    /// <summary>Matches a Nix path literal — at least one slash, plus path-body bytes.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchPathLiteral(ReadOnlySpan<byte> slice)
    {
        var pathStart = ConsumePathLeader(slice);
        if (pathStart is 0)
        {
            return 0;
        }

        var stop = slice[pathStart..].IndexOfAnyExcept(PathContinue);
        var bodyLen = stop < 0 ? slice.Length - pathStart : stop;
        return bodyLen is 0 ? 0 : pathStart + bodyLen;
    }

    /// <summary>Consumes the leading <c>~/</c>, <c>./</c>, <c>../</c>, or bare <c>/</c> portion of a Nix path literal. The body bytes start immediately after.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Number of bytes consumed, or zero on miss.</returns>
    private static int ConsumePathLeader(ReadOnlySpan<byte> slice)
    {
        const int TildeSlashLength = 2;
        const int DotSlashLength = 2;
        const int DotDotSlashLength = 3;
        return slice switch
        {
            [(byte)'~', (byte)'/', ..] => TildeSlashLength,
            [(byte)'.', (byte)'.', (byte)'/', ..] => DotDotSlashLength,
            [(byte)'.', (byte)'/', ..] => DotSlashLength,
            [(byte)'/', ..] => 1,
            _ => 0
        };
    }
}
