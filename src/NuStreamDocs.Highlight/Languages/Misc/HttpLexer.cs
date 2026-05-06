// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Misc;

/// <summary>HTTP request / response lexer.</summary>
/// <remarks>
/// Recognizes the request line (<c>GET /path HTTP/1.1</c>), the response status
/// line (<c>HTTP/1.1 200 OK</c>), the header lines (<c>Header-Name: value</c>),
/// and the empty separator line. Body bytes after the empty line pass through
/// as plain text — embedded JSON / form-data / etc. isn't separately
/// classified at the lexer level.
/// </remarks>
public static class HttpLexer
{
    /// <summary>Common HTTP method names — recognized at line start.</summary>
    private static readonly ByteKeywordSet Methods = ByteKeywordSet.Create(
        [.. "GET"u8],
        [.. "POST"u8],
        [.. "PUT"u8],
        [.. "PATCH"u8],
        [.. "DELETE"u8],
        [.. "HEAD"u8],
        [.. "OPTIONS"u8],
        [.. "TRACE"u8],
        [.. "CONNECT"u8],
        [.. "PROPFIND"u8],
        [.. "PROPPATCH"u8],
        [.. "MKCOL"u8],
        [.. "COPY"u8],
        [.. "MOVE"u8],
        [.. "LOCK"u8],
        [.. "UNLOCK"u8]);

    /// <summary>First-byte set for the method-line rule (uppercase ASCII letters).</summary>
    private static readonly SearchValues<byte> MethodFirst = SearchValues.Create("CDGHLMOPTU"u8);

    /// <summary>First-byte set for whitespace.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>Identifier-continuation set for HTTP header names — letters / digits / dash.</summary>
    private static readonly SearchValues<byte> HeaderNameContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create(":;,/?&=()[]"u8);

    /// <summary>Gets the singleton HTTP lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the HTTP lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },

            // GET /path HTTP/1.1 — the whole request line classifies as one keyword token.
            new(MatchRequestLine, TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = MethodFirst, RequiresLineStart = true },

            // HTTP/1.1 200 OK — the whole status line classifies as one keyword token.
            new(MatchStatusLine, TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = SearchValues.Create("H"u8), RequiresLineStart = true },

            // Header-Name: value — emit the name as Name.Attribute, value falls through.
            new(MatchHeaderName, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart, RequiresLineStart = true },

            // "..." string with backslash escapes (rare in HTTP but appears in JSON-shape bodies).
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

            // Numbers (status codes, content-length values, etc.).
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

            // Bare identifier (header-value tokens, URL segments).
            new(
                static slice => TokenMatchers.MatchIdentifier(slice, TokenMatchers.AsciiIdentifierStart, HeaderNameContinue),
                TokenClass.Name,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

            // Single-byte structural punctuation.
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches an HTTP request line — <c>METHOD path HTTP/version</c> through to end-of-line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchRequestLine(ReadOnlySpan<byte> slice)
    {
        var methodLen = TokenMatchers.MatchKeyword(slice, Methods);
        if (methodLen is 0)
        {
            return 0;
        }

        // Method must be followed by a space and HTTP/ at some later point on the same line.
        if (methodLen >= slice.Length || slice[methodLen] is not (byte)' ')
        {
            return 0;
        }

        var lineLen = TokenMatchers.LineLength(slice);
        return slice[..lineLen].IndexOf("HTTP/"u8) >= 0 ? lineLen : 0;
    }

    /// <summary>Matches an HTTP status line — <c>HTTP/version status reason</c> through to end-of-line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchStatusLine(ReadOnlySpan<byte> slice) =>
        slice.StartsWith("HTTP/"u8) ? TokenMatchers.LineLength(slice) : 0;

    /// <summary>Matches an HTTP header name — identifier with dashes followed by <c>:</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length of the header name (the colon stays for the punctuation rule), or zero.</returns>
    private static int MatchHeaderName(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || !TokenMatchers.AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var stop = slice[1..].IndexOfAnyExcept(HeaderNameContinue);
        var nameLen = stop < 0 ? slice.Length : 1 + stop;
        return nameLen < slice.Length && slice[nameLen] is (byte)':' ? nameLen : 0;
    }
}
