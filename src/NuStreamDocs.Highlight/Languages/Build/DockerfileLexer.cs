// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Build;

/// <summary>Dockerfile lexer.</summary>
/// <remarks>
/// Line-anchored verb classification (<c>FROM</c>, <c>RUN</c>, <c>CMD</c>, …)
/// followed by a free-form rest-of-line. Comments use <c>#</c>; double-quoted
/// strings classify as <c>s2</c>. Variable references like <c>${VAR}</c> stay
/// inside the surrounding text token — the surface form still lights up but
/// the inner expression isn't re-entered without a state stack.
/// </remarks>
public static class DockerfileLexer
{
    /// <summary>Recognized Dockerfile instruction verbs (uppercase).</summary>
    private static readonly ByteKeywordSet Instructions = ByteKeywordSet.CreateIgnoreCase(
        [.. "from"u8],
        [.. "as"u8],
        [.. "run"u8],
        [.. "cmd"u8],
        [.. "label"u8],
        [.. "maintainer"u8],
        [.. "expose"u8],
        [.. "env"u8],
        [.. "add"u8],
        [.. "copy"u8],
        [.. "entrypoint"u8],
        [.. "volume"u8],
        [.. "user"u8],
        [.. "workdir"u8],
        [.. "arg"u8],
        [.. "onbuild"u8],
        [.. "stopsignal"u8],
        [.. "healthcheck"u8],
        [.. "shell"u8]);

    /// <summary>First-byte dispatch set for the verb rule.</summary>
    private static readonly SearchValues<byte> InstructionFirst = SearchValues.Create("ABCDEFHLMORSUVWabcdefhlmorsuvw"u8);

    /// <summary>First-byte set for whitespace runs.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = SearchValues.Create(" \t\r\n"u8);

    /// <summary>First-byte set for hash-prefixed comments.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for double-quoted strings.</summary>
    private static readonly SearchValues<byte> DoubleQuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for single-quoted strings.</summary>
    private static readonly SearchValues<byte> SingleQuoteFirst = SearchValues.Create("'"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,=:\\"u8);

    /// <summary>Gets the singleton Dockerfile lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Dockerfile lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },
            new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },
            new(MatchInstructionAtLineStart, TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = InstructionFirst, RequiresLineStart = true },
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DoubleQuoteFirst },
            new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''), TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = SingleQuoteFirst },
            new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches a Dockerfile instruction verb at the start of a line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchInstructionAtLineStart(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchKeyword(slice, Instructions);
}
