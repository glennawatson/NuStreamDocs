// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Lua lexer.</summary>
/// <remarks>
/// Custom shape — <c>--</c> line comments, <c>--[[ ... ]]</c> block comments
/// (with optional level markers <c>--[==[ ... ]==]</c>), and the matching
/// <c>[[ ... ]]</c> long-string literal.
/// </remarks>
public static class LuaLexer
{
    /// <summary>Length of the <c>--</c> line-comment introducer.</summary>
    private const int LineCommentPrefixLength = 2;

    /// <summary>General-keyword set.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "then"u8],
        [.. "else"u8],
        [.. "elseif"u8],
        [.. "end"u8],
        [.. "for"u8],
        [.. "while"u8],
        [.. "do"u8],
        [.. "repeat"u8],
        [.. "until"u8],
        [.. "break"u8],
        [.. "goto"u8],
        [.. "return"u8],
        [.. "in"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8]);

    /// <summary>Declaration keywords.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "function"u8],
        [.. "local"u8]);

    /// <summary>Constant keywords.</summary>
    private static readonly ByteKeywordSet KeywordConstants = ByteKeywordSet.Create(
        [.. "true"u8],
        [.. "false"u8],
        [.. "nil"u8]);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. "..."u8],
        [.. ".."u8],
        [.. "=="u8],
        [.. "~="u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "^"u8],
        [.. "#"u8],
        [.. "="u8],
        [.. "<"u8],
        [.. ">"u8]
    ];

    /// <summary>First-byte set for whitespace runs.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for comments / long-string introducers (<c>-</c> for line / block, <c>[</c> for long strings).</summary>
    private static readonly SearchValues<byte> DashFirst = SearchValues.Create("-"u8);

    /// <summary>First-byte set for the long-string rule.</summary>
    private static readonly SearchValues<byte> BracketFirst = SearchValues.Create("["u8);

    /// <summary>First-byte set for double-quoted strings.</summary>
    private static readonly SearchValues<byte> DoubleQuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for single-quoted strings.</summary>
    private static readonly SearchValues<byte> SingleQuoteFirst = SearchValues.Create("'"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("abdefginortuw"u8);

    /// <summary>First-byte set for declaration keywords.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("fl"u8);

    /// <summary>First-byte set for constant keywords.</summary>
    private static readonly SearchValues<byte> KeywordConstantFirst = SearchValues.Create("tfn"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("+-*/%^#=<>~."u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){},;:."u8);

    /// <summary>Gets the singleton Lua lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Lua lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },

            // --[[ ... ]] block comment — must precede the line-comment rule.
            new(MatchDashBlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = DashFirst },

            // -- line comment to end-of-line.
            new(MatchDashLineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = DashFirst },

            // [[...]] long-string literal.
            new(MatchLongString, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = BracketFirst },

            // Regular strings.
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DoubleQuoteFirst },
            new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''), TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = SingleQuoteFirst },

            // Numbers.
            new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

            // Keywords.
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordConstants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = KeywordConstantFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, KeywordDeclarations), TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst },
            new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst },

            new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },
            new(static slice => TokenMatchers.MatchLongestLiteral(slice, OperatorTable), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches a Lua block comment <c>--[[ ... ]]</c> (with optional <c>=</c> level markers).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchDashBlockComment(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < LineCommentPrefixLength || slice[0] is not (byte)'-' || slice[1] is not (byte)'-')
        {
            return 0;
        }

        if (slice.Length <= LineCommentPrefixLength || slice[LineCommentPrefixLength] is not (byte)'[')
        {
            return 0;
        }

        var blockBody = MatchLongStringBody(slice[LineCommentPrefixLength..]);
        return blockBody is 0 ? 0 : LineCommentPrefixLength + blockBody;
    }

    /// <summary>Matches a Lua <c>--</c> line comment to end-of-line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchDashLineComment(ReadOnlySpan<byte> slice)
    {
        if (slice.Length < LineCommentPrefixLength || slice[0] is not (byte)'-' || slice[1] is not (byte)'-')
        {
            return 0;
        }

        return LineCommentPrefixLength + TokenMatchers.LineLength(slice[LineCommentPrefixLength..]);
    }

    /// <summary>Matches a Lua long-string literal <c>[[ ... ]]</c> with optional level markers <c>[==[ ... ]==]</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchLongString(ReadOnlySpan<byte> slice) =>
        slice is [(byte)'[', ..] ? MatchLongStringBody(slice) : 0;

    /// <summary>Matches the long-bracket body: <c>[</c> + level <c>=</c>s + <c>[</c> + body + <c>]</c> + matching <c>=</c>s + <c>]</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor (already known to start with <c>[</c>).</param>
    /// <returns>Length matched on success, or zero on miss / unterminated input.</returns>
    private static int MatchLongStringBody(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'[')
        {
            return 0;
        }

        var pos = 1;
        while (pos < slice.Length && slice[pos] is (byte)'=')
        {
            pos++;
        }

        if (pos >= slice.Length || slice[pos] is not (byte)'[')
        {
            return 0;
        }

        var levelCount = pos - 1;
        return ScanLongStringClose(slice, pos + 1, levelCount);
    }

    /// <summary>Walks a long-string body until a matching <c>]</c> + <paramref name="levelCount"/> <c>=</c>s + <c>]</c> closer.</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="bodyStart">Index of the first body byte (after the opening <c>[</c>).</param>
    /// <param name="levelCount">Number of <c>=</c> bytes that must appear between the closing brackets.</param>
    /// <returns>Total length matched on success, or zero on unterminated input.</returns>
    private static int ScanLongStringClose(ReadOnlySpan<byte> slice, int bodyStart, int levelCount)
    {
        var pos = bodyStart;
        while (pos < slice.Length)
        {
            if (slice[pos] is not (byte)']')
            {
                pos++;
                continue;
            }

            var closeEnd = TryMatchLongStringClose(slice, pos, levelCount);
            if (closeEnd > 0)
            {
                return closeEnd;
            }

            pos++;
        }

        return 0;
    }

    /// <summary>Tests whether a closing <c>]</c> at <paramref name="closeStart"/> is followed by exactly <paramref name="levelCount"/> <c>=</c>s and another <c>]</c>.</summary>
    /// <param name="slice">Original slice anchored at the cursor.</param>
    /// <param name="closeStart">Index of the candidate first <c>]</c>.</param>
    /// <param name="levelCount">Required <c>=</c> count.</param>
    /// <returns>One past the closing <c>]</c> on success, zero on miss.</returns>
    private static int TryMatchLongStringClose(ReadOnlySpan<byte> slice, int closeStart, int levelCount)
    {
        var probe = closeStart + 1;
        var matched = 0;
        while (matched < levelCount && probe < slice.Length && slice[probe] is (byte)'=')
        {
            matched++;
            probe++;
        }

        return matched == levelCount && probe < slice.Length && slice[probe] is (byte)']' ? probe + 1 : 0;
    }
}
