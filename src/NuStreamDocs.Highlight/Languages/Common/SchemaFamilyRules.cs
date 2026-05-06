// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Reusable schema-shape lexer rule builder.</summary>
/// <remarks>
/// Single-state lexer covering the flat declaration / value shape that
/// GraphQL, Protobuf, HCL, Thrift, Cue, and JSON-Schema all share —
/// configurable comment style (<c>#</c> and / or <c>//</c>), optional
/// triple-quoted descriptions, optional sigil-prefixed names
/// (<c>$variable</c>, <c>@directive</c>), and per-language keyword tables.
/// </remarks>
internal static class SchemaFamilyRules
{
    /// <summary>First-byte set for whitespace runs (with newlines).</summary>
    public static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for hash-prefixed comments.</summary>
    public static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>Minimum opening / closing quote run for a triple-quoted block-string description.</summary>
    private const int TripleQuoteLength = 3;

    /// <summary>Builds a single-state schema-family <see cref="Lexer"/> from <paramref name="config"/> in one call.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Built lexer.</returns>
    public static Lexer CreateLexer(in SchemaFamilyConfig config) =>
        new(LanguageRuleBuilder.BuildSingleState(Build(config)));

    /// <summary>Builds the schema-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in SchemaFamilyConfig config)
    {
        const int MaxRuleSlots = 16;
        var rules = new List<LexerRule>(MaxRuleSlots)
        {
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst }
        };

        if (config.IncludeHashComment)
        {
            rules.Add(new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst });
        }

        if (config.IncludeSlashComments)
        {
            rules.Add(new(LanguageCommon.LineComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst });
            rules.Add(new(LanguageCommon.BlockComment, TokenClass.CommentMulti, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SlashFirst });
        }

        if (config.IncludeTripleQuotedString)
        {
            // """description""" must precede the regular string rule.
            rules.Add(new(
                static slice => TokenMatchers.MatchRawQuotedString(slice, (byte)'"', TripleQuoteLength),
                TokenClass.StringDouble,
                LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst });
        }

        rules.Add(new(
            TokenMatchers.MatchDoubleQuotedWithBackslashEscape,
            TokenClass.StringDouble,
            LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst });

        if (config.IncludeSingleQuotedString)
        {
            rules.Add(new(
                static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''),
                TokenClass.StringSingle,
                LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst });
        }

        // $variable / @directive sigil-prefixed name.
        if (config.SigilFirst is { } sigilFirst)
        {
            rules.Add(new(MatchSigilName, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = sigilFirst });
        }

        rules.Add(new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });
        rules.Add(new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });

        rules.Add(BuildKeywordRule(config.KeywordConstants, config.KeywordConstantFirst, TokenClass.KeywordConstant));
        rules.Add(BuildKeywordRule(config.KeywordTypes, config.KeywordTypeFirst, TokenClass.KeywordType));
        rules.Add(BuildKeywordRule(config.KeywordDeclarations, config.KeywordDeclarationFirst, TokenClass.KeywordDeclaration));
        rules.Add(BuildKeywordRule(config.Keywords, config.KeywordFirst, TokenClass.Keyword));

        rules.Add(new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart });

        if (config.Operators is { } operators)
        {
            var opFirst = config.OperatorFirst ?? OperatorAlternationFactory.FirstBytesOf(operators);
            rules.Add(new(slice => TokenMatchers.MatchLongestLiteral(slice, operators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = opFirst });
        }

        var punctuation = config.Punctuation;
        rules.Add(new(slice => TokenMatchers.MatchSingleByteOf(slice, punctuation), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = punctuation });

        return [.. rules];
    }

    /// <summary>Builds a keyword-set rule, falling back to the auto-derived first-byte set when no override is supplied.</summary>
    /// <param name="keywords">Keyword set.</param>
    /// <param name="firstBytes">Optional first-byte dispatch set.</param>
    /// <param name="tokenClass">Classification.</param>
    /// <returns>Rule matching any member of <paramref name="keywords"/>.</returns>
    private static LexerRule BuildKeywordRule(ByteKeywordSet keywords, SearchValues<byte>? firstBytes, TokenClass tokenClass)
    {
        var captured = keywords;
        return new(slice => TokenMatchers.MatchKeyword(slice, captured), tokenClass, LexerRule.NoStateChange) { FirstBytes = firstBytes ?? captured.FirstByteSet };
    }

    /// <summary>Matches a sigil + identifier token (<c>$variable</c>, <c>@directive</c>, <c>:atom</c>).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (sigil + body), or zero.</returns>
    private static int MatchSigilName(ReadOnlySpan<byte> slice)
    {
        if (slice is [])
        {
            return 0;
        }

        var ident = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        return ident is 0 ? 0 : 1 + ident;
    }
}
