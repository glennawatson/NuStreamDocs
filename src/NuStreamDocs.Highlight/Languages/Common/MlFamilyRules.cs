// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Reusable ML-family lexer rule builder.</summary>
/// <remarks>
/// Generalizes the shape shared by OCaml, Haskell, F#, ReasonML, and Elm — nested block
/// comments, optional line comments, type variables (<c>'a</c>), and a kept-flat keyword
/// table. The block-comment matcher tracks delimiter depth so <c>(* outer (* inner *) *)</c>
/// closes correctly.
/// </remarks>
internal static class MlFamilyRules
{
    /// <summary>First-byte set for whitespace runs (with newlines).</summary>
    public static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for double-quoted strings.</summary>
    public static readonly SearchValues<byte> DoubleQuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for single-quoted character literals or type variables.</summary>
    public static readonly SearchValues<byte> SingleQuoteFirst = SearchValues.Create("'"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    public static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){}[];,."u8);

    /// <summary>Length of the two-byte block-comment opener / closer.</summary>
    private const int BlockDelimiterLength = 2;

    /// <summary>Builds a single-state ML-family <see cref="Lexer"/> from <paramref name="config"/> in one call.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Built lexer.</returns>
    public static Lexer CreateLexer(in MlFamilyConfig config) =>
        new(LanguageRuleBuilder.BuildSingleState(Build(config)));

    /// <summary>Builds the ML-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in MlFamilyConfig config)
    {
        const int MaxRuleSlots = 16;
        var rules = new List<LexerRule>(MaxRuleSlots)
        {
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst }
        };

        var blockOpen = config.BlockCommentOpen;
        var blockClose = config.BlockCommentClose;
        var blockOpenFirst = SearchValues.Create(blockOpen.AsSpan(0, 1));
        rules.Add(new(slice => MatchNestedBlockComment(slice, blockOpen, blockClose), TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = blockOpenFirst });

        if (config.LineCommentPrefix is { } linePrefix)
        {
            var lineFirst = SearchValues.Create(linePrefix.AsSpan(0, 1));
            rules.Add(new(slice => MatchLineComment(slice, linePrefix), TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = lineFirst });
        }

        // 'a / 'b type variable — single quote followed by an identifier with no closing quote.
        rules.Add(new(MatchTypeVariableOrCharLiteral, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = SingleQuoteFirst });

        // "..." double-quoted string with backslash escapes.
        rules.Add(new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DoubleQuoteFirst });

        // 1.0 float / 1 integer.
        rules.Add(new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });
        rules.Add(new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });

        // Keyword tables (case-sensitive).
        var constants = config.KeywordConstants;
        var types = config.KeywordTypes;
        var declarations = config.KeywordDeclarations;
        var keywords = config.Keywords;
        rules.Add(new(slice => TokenMatchers.MatchKeyword(slice, constants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = config.KeywordConstantFirst });
        rules.Add(new(slice => TokenMatchers.MatchKeyword(slice, types), TokenClass.KeywordType, LexerRule.NoStateChange) { FirstBytes = config.KeywordTypeFirst });
        rules.Add(new(slice => TokenMatchers.MatchKeyword(slice, declarations), TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = config.KeywordDeclarationFirst });
        rules.Add(new(slice => TokenMatchers.MatchKeyword(slice, keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = config.KeywordFirst });

        // Identifier — letters / digits / underscore / trailing apostrophe (ML convention).
        rules.Add(new(MatchMlIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart });

        // Operator alternation.
        var operators = config.Operators;
        rules.Add(new(slice => TokenMatchers.MatchLongestLiteral(slice, operators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = config.OperatorFirst });

        rules.Add(new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet });

        return [.. rules];
    }

    /// <summary>Matches a depth-tracking block comment with the configured open / close delimiters.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="open">Two-byte opener (e.g. <c>(*</c>).</param>
    /// <param name="close">Two-byte closer (e.g. <c>*)</c>).</param>
    /// <returns>Length matched on success, or zero on miss / unterminated input.</returns>
    private static int MatchNestedBlockComment(ReadOnlySpan<byte> slice, ReadOnlySpan<byte> open, ReadOnlySpan<byte> close)
    {
        if (!slice.StartsWith(open))
        {
            return 0;
        }

        var pos = open.Length;
        var depth = 1;
        while (pos < slice.Length)
        {
            if (pos + open.Length <= slice.Length && slice.Slice(pos, open.Length).SequenceEqual(open))
            {
                pos += open.Length;
                depth++;
                continue;
            }

            if (pos + close.Length <= slice.Length && slice.Slice(pos, close.Length).SequenceEqual(close))
            {
                pos += close.Length;
                depth--;
                if (depth is 0)
                {
                    return pos;
                }

                continue;
            }

            pos++;
        }

        return 0;
    }

    /// <summary>Matches a configurable line comment — prefix bytes followed by anything to end-of-line.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefix">Line-comment prefix bytes (typically two characters).</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchLineComment(ReadOnlySpan<byte> slice, ReadOnlySpan<byte> prefix)
    {
        if (!slice.StartsWith(prefix))
        {
            return 0;
        }

        return prefix.Length + TokenMatchers.LineLength(slice[prefix.Length..]);
    }

    /// <summary>Matches an ML type variable (<c>'a</c>, <c>'foo</c>) or a character literal (<c>'x'</c>, <c>'\n'</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchTypeVariableOrCharLiteral(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'\'')
        {
            return 0;
        }

        // Char-literal forms first — 'x' (3 bytes) or '\x' (4 bytes).
        var charLen = LanguageCommon.CharLiteral(slice);
        if (charLen > 0)
        {
            return charLen;
        }

        // 'a / 'b / 'foo type variable.
        if (slice.Length < BlockDelimiterLength || !TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return 0;
        }

        var stop = slice[BlockDelimiterLength..].IndexOfAnyExcept(TokenMatchers.AsciiIdentifierContinue);
        return stop < 0 ? slice.Length : 1 + 1 + stop;
    }

    /// <summary>Matches an ML identifier — leading letter / underscore, then letters / digits / underscore / trailing apostrophes.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchMlIdentifier(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || !TokenMatchers.AsciiIdentifierStart.Contains(slice[0]))
        {
            return 0;
        }

        var pos = 1;
        while (pos < slice.Length && TokenMatchers.AsciiIdentifierContinue.Contains(slice[pos]))
        {
            pos++;
        }

        // Trailing apostrophes — common in ML-family code (`x'`, `next''`).
        while (pos < slice.Length && slice[pos] is (byte)'\'')
        {
            pos++;
        }

        return pos;
    }
}
