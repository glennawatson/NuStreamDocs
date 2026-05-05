// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Reusable Lisp-family lexer rule builder.</summary>
/// <remarks>
/// Single-state lexer covering S-expressions: <c>;</c> line comments, <c>#|...|#</c>
/// block comments (Common Lisp / Scheme), parenthesized lists, quoted forms,
/// character literals (<c>#\x</c>), strings, symbols (with the broad Lisp identifier
/// alphabet — <c>!</c>, <c>?</c>, <c>-</c>, <c>+</c>, <c>*</c>, <c>/</c>, <c>&lt;</c>, <c>&gt;</c>),
/// keyword tables, and the optional Clojure / EDN <c>[]</c>/<c>{}</c> data
/// brackets and <c>:keyword</c> literals.
/// </remarks>
internal static class LispFamilyRules
{
    /// <summary>First-byte set for whitespace runs (with newlines).</summary>
    public static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the line-comment rule (<c>;</c>).</summary>
    public static readonly SearchValues<byte> SemicolonFirst = SearchValues.Create(";"u8);

    /// <summary>First-byte set for double-quoted strings.</summary>
    public static readonly SearchValues<byte> DoubleQuoteFirst = SearchValues.Create("\""u8);

    /// <summary>First-byte set for the <c>#</c> dispatch rule (block comment, character literal, datum comment).</summary>
    public static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for quote / quasiquote / unquote prefixes.</summary>
    public static readonly SearchValues<byte> QuoteFirst = SearchValues.Create("'`,"u8);

    /// <summary>First-byte set for the colon-keyword rule.</summary>
    public static readonly SearchValues<byte> ColonFirst = SearchValues.Create(":"u8);

    /// <summary>Symbol-continuation set — letters, digits, and the broad Lisp punctuation alphabet.</summary>
    public static readonly SearchValues<byte> SymbolContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-+*/?!<>=.&%$"u8);

    /// <summary>Symbol-start set — letters, underscore, and operator-shaped Lisp symbol leaders.</summary>
    public static readonly SearchValues<byte> SymbolStart = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_-+*/?!<>=&%$"u8);

    /// <summary>Length of a colon-keyword lookahead pair (<c>:</c> + start byte).</summary>
    private const int ColonAndStartLength = 2;

    /// <summary>Length of the <c>#;</c> / <c>#_</c> datum-comment marker.</summary>
    private const int DatumCommentMarkerLength = 2;

    /// <summary>Single-byte structural punctuation. Brackets are configured per-dialect.</summary>
    private static readonly SearchValues<byte> ParenPunctuation = SearchValues.Create("()"u8);

    /// <summary>Bracket / brace / paren punctuation for Clojure-flavored dialects.</summary>
    private static readonly SearchValues<byte> ClojurePunctuation = SearchValues.Create("()[]{}"u8);

    /// <summary>Builds the Lisp-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in LispFamilyConfig config)
    {
        const int MaxRuleSlots = 14;
        var rules = new List<LexerRule>(MaxRuleSlots)
        {
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },

            // ; line comment to end-of-line.
            new(static slice => TokenMatchers.MatchLineCommentToEol(slice, (byte)';'), TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = SemicolonFirst },

            // #|...|# block comment, #\char character literal, #;datum-comment marker — all dispatched off the leading '#'.
            new(MatchHashDispatch, TokenClass.Text, LexerRule.NoStateChange) { FirstBytes = HashFirst },

            // "..." string with backslash escapes.
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = DoubleQuoteFirst },

            // ' / ` / , quote prefix.
            new(MatchQuotePrefix, TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = QuoteFirst }
        };

        if (config.IncludeColonKeyword)
        {
            rules.Add(new(MatchColonKeyword, TokenClass.NameAttribute, LexerRule.NoStateChange) { FirstBytes = ColonFirst });
        }

        // Numeric literal — float first.
        rules.Add(new(TokenMatchers.MatchUnsignedAsciiFloat, TokenClass.NumberFloat, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });
        rules.Add(new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits });

        // Keyword tables (case-sensitive). Constants first so `t` / `nil` win over the symbol rule.
        var constants = config.KeywordConstants;
        var declarations = config.KeywordDeclarations;
        var keywords = config.Keywords;
        rules.Add(new(slice => MatchSymbolKeyword(slice, constants), TokenClass.KeywordConstant, LexerRule.NoStateChange) { FirstBytes = config.KeywordConstantFirst });
        rules.Add(new(slice => MatchSymbolKeyword(slice, declarations), TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = config.KeywordDeclarationFirst });
        rules.Add(new(slice => MatchSymbolKeyword(slice, keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = config.KeywordFirst });

        // Bare symbol — letters, digits, and the broad Lisp punctuation alphabet.
        rules.Add(new(MatchSymbol, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = SymbolStart });

        // Brackets / parens.
        var punctuation = config.IncludeDataBrackets ? ClojurePunctuation : ParenPunctuation;
        rules.Add(new(slice => TokenMatchers.MatchSingleByteOf(slice, punctuation), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = punctuation });

        return [.. rules];
    }

    /// <summary>
    /// Matches forms introduced by <c>#</c>: <c>#|...|#</c> block comments,
    /// <c>#\x</c> character literals, and the <c>#;</c> / <c>#_</c> datum-comment markers.
    /// </summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchHashDispatch(ReadOnlySpan<byte> slice) => slice switch
    {
        [(byte)'#', (byte)'|', ..] => MatchBlockComment(slice),
        [(byte)'#', (byte)'\\', _, ..] => MatchCharacterLiteral(slice),
        [(byte)'#', (byte)';', ..] => DatumCommentMarkerLength,
        [(byte)'#', (byte)'_', ..] => DatumCommentMarkerLength,
        _ => 0
    };

    /// <summary>Matches a Common-Lisp / Scheme block comment <c>#| ... |#</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor (already known to start with <c>#|</c>).</param>
    /// <returns>Length matched, or zero on unterminated input.</returns>
    private static int MatchBlockComment(ReadOnlySpan<byte> slice)
    {
        const int OpenLength = 2;
        const int CloseLength = 2;
        var rest = slice[OpenLength..];
        var close = rest.IndexOf("|#"u8);
        return close < 0 ? 0 : OpenLength + close + CloseLength;
    }

    /// <summary>Matches a Common-Lisp / Scheme character literal — <c>#\x</c>, <c>#\space</c>, <c>#\newline</c>, and similar named-character forms.</summary>
    /// <param name="slice">Slice anchored at the cursor (already known to start with <c>#\</c>).</param>
    /// <returns>Length matched.</returns>
    private static int MatchCharacterLiteral(ReadOnlySpan<byte> slice)
    {
        const int PrefixLength = 2;
        var pos = PrefixLength;
        if (pos >= slice.Length)
        {
            return PrefixLength;
        }

        // Always consume the first body byte; if it's followed by an identifier run (named character),
        // grab those too.
        pos++;
        if (pos < slice.Length && SymbolContinue.Contains(slice[pos]))
        {
            var stop = slice[pos..].IndexOfAnyExcept(SymbolContinue);
            pos = stop < 0 ? slice.Length : pos + stop;
        }

        return pos;
    }

    /// <summary>Matches a quote prefix — single byte <c>'</c> / <c>`</c> / <c>,</c> (with optional <c>@</c> for unquote-splicing).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched (1 or 2).</returns>
    private static int MatchQuotePrefix(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || (slice[0] is not (byte)'\'' && slice[0] is not (byte)'`' && slice[0] is not (byte)','))
        {
            return 0;
        }

        // ,@ unquote-splicing.
        if (slice.Length > 1 && slice[0] is (byte)',' && slice[1] is (byte)'@')
        {
            return ColonAndStartLength;
        }

        return 1;
    }

    /// <summary>Matches a colon-keyword literal <c>:foo</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchColonKeyword(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)':')
        {
            return 0;
        }

        if (slice.Length < ColonAndStartLength || !SymbolStart.Contains(slice[1]))
        {
            return 0;
        }

        var stop = slice[ColonAndStartLength..].IndexOfAnyExcept(SymbolContinue);
        return stop < 0 ? slice.Length : ColonAndStartLength + stop;
    }

    /// <summary>Matches a bare Lisp symbol — start byte then continuation bytes.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchSymbol(ReadOnlySpan<byte> slice) =>
        TokenMatchers.MatchIdentifier(slice, SymbolStart, SymbolContinue);

    /// <summary>Matches one of the keywords in <paramref name="keywords"/> followed by a non-symbol-continue byte (or end-of-slice).</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="keywords">Keyword set.</param>
    /// <returns>Length of the matched keyword, or zero.</returns>
    private static int MatchSymbolKeyword(ReadOnlySpan<byte> slice, ByteKeywordSet keywords)
    {
        if (slice is [] || !SymbolStart.Contains(slice[0]))
        {
            return 0;
        }

        var endRel = slice[1..].IndexOfAnyExcept(SymbolContinue);
        var end = endRel < 0 ? slice.Length : 1 + endRel;
        return keywords.Contains(slice[..end]) ? end : 0;
    }
}
