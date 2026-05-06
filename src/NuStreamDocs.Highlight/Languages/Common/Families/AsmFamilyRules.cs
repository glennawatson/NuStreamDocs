// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Reusable assembly-family lexer rule builder.</summary>
/// <remarks>
/// Assembly dialects share the line-based shape: optional <c>label:</c>, an
/// optional <c>.directive</c>, a mnemonic, then comma-separated operands.
/// Each dialect plugs in its mnemonic / register tables and the comment
/// introducer; the rest is the same across x86 / ARM / MIPS / RISC-V / WAT /
/// 6502 / Z80 / etc. Mnemonic and register lookups are case-insensitive.
/// </remarks>
internal static class AsmFamilyRules
{
    /// <summary>First-byte set for whitespace runs.</summary>
    public static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the <c>.directive</c> rule.</summary>
    public static readonly SearchValues<byte> DotFirst = SearchValues.Create("."u8);

    /// <summary>Identifier-continuation set — assembly identifiers may contain <c>.</c> and <c>$</c> for local labels and macros.</summary>
    public static readonly SearchValues<byte> AsmIdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_.$"u8);

    /// <summary>Single-byte structural punctuation (comma, brackets, parens, plus, minus, colon for label trailer).</summary>
    public static readonly SearchValues<byte> PunctuationFirst = SearchValues.Create(",[]()+-:#"u8);

    /// <summary>Hex-body bytes for <c>0x...</c> literals.</summary>
    public static readonly SearchValues<byte> HexBody = SearchValues.Create("0123456789abcdefABCDEF"u8);

    /// <summary>Builds a single-state assembly <see cref="Lexer"/> from <paramref name="config"/> in one call.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Built lexer.</returns>
    public static Lexer CreateLexer(in AsmFamilyConfig config) =>
        new(LanguageRuleBuilder.BuildSingleState(Build(config)));

    /// <summary>Builds the assembly-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in AsmFamilyConfig config)
    {
        var commentFirst = config.CommentFirst;
        var mnemonics = config.Mnemonics;
        var registers = config.Registers;
        var hexPrefix = config.HexPrefix;

        return
        [
            new(
                TokenMatchers.MatchAsciiWhitespace,
                TokenClass.Whitespace,
                LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },

            // Line comment to end-of-line.
            new(
                slice => MatchLineComment(slice, commentFirst),
                TokenClass.CommentSingle,
                LexerRule.NoStateChange) { FirstBytes = commentFirst },

            // "..." string with backslash escapes.
            new(
                TokenMatchers.MatchDoubleQuotedWithBackslashEscape,
                TokenClass.StringDouble,
                LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

            // 0x... hex literal.
            new(
                slice => MatchHexLiteral(slice, hexPrefix),
                TokenClass.NumberHex,
                LexerRule.NoStateChange) { FirstBytes = LanguageCommon.HexFirst },

            // Decimal literal.
            new(
                TokenMatchers.MatchAsciiDigits,
                TokenClass.NumberInteger,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

            // .directive (line-anchored after whitespace).
            new(
                MatchDirective,
                TokenClass.CommentPreproc,
                LexerRule.NoStateChange) { FirstBytes = DotFirst },

            // Register name (case-insensitive).
            new(
                slice => TokenMatchers.MatchKeyword(slice, registers),
                TokenClass.NameBuiltin,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

            // Mnemonic / opcode (case-insensitive).
            new(
                slice => TokenMatchers.MatchKeyword(slice, mnemonics),
                TokenClass.Keyword,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

            // Bare identifier — labels, macro names, operand symbols.
            new(
                slice => TokenMatchers.MatchIdentifier(slice, TokenMatchers.AsciiIdentifierStart, AsmIdentifierContinue),
                TokenClass.Name,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

            // Single-byte punctuation.
            new(
                static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationFirst),
                TokenClass.Punctuation,
                LexerRule.NoStateChange) { FirstBytes = PunctuationFirst }
        ];
    }

    /// <summary>Matches a line comment whose introducer byte is in <paramref name="prefixSet"/>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="prefixSet">Allowed comment-introducer bytes.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchLineComment(ReadOnlySpan<byte> slice, SearchValues<byte> prefixSet)
    {
        if (slice is [] || !prefixSet.Contains(slice[0]))
        {
            return 0;
        }

        return TokenMatchers.LineLength(slice);
    }

    /// <summary>Matches a hex literal — <c>0x...</c> when enabled.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="hexPrefix">Whether to recognize the <c>0x</c> prefix form.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchHexLiteral(ReadOnlySpan<byte> slice, bool hexPrefix)
    {
        if (!hexPrefix)
        {
            return 0;
        }

        return TokenMatchers.MatchAsciiHexLiteral(slice, HexBody, CFamilyRules.NoSuffix);
    }

    /// <summary>Matches an assembly directive — <c>.</c> followed by an identifier body (<c>.section</c>, <c>.global</c>, <c>.byte</c>, …).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchDirective(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchPrefixedRun(slice, (byte)'.', AsmIdentifierContinue);
}
