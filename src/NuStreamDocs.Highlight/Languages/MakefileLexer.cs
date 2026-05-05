// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>GNU Make / BSD Make lexer.</summary>
/// <remarks>
/// <c>#</c> line comments, <c>$(VAR)</c> / <c>${VAR}</c> / <c>$@</c>-style variable
/// expansions, conditional directives (<c>ifeq</c>, <c>ifneq</c>, <c>endif</c>, …),
/// the <c>include</c> / <c>export</c> / <c>define</c> directive set, and recipe-line
/// detection via the <c>RequiresLineStart</c> tab-prefix rule.
/// </remarks>
public static class MakefileLexer
{
    /// <summary>Conditional / control directives.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "ifeq"u8],
        [.. "ifneq"u8],
        [.. "ifdef"u8],
        [.. "ifndef"u8],
        [.. "else"u8],
        [.. "endif"u8],
        [.. "if"u8],
        [.. "or"u8],
        [.. "and"u8]);

    /// <summary>Declaration / module directives. The <c>-include</c> form is matched separately because <c>MatchKeyword</c> requires an ASCII-identifier start byte.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.Create(
        [.. "include"u8],
        [.. "sinclude"u8],
        [.. "export"u8],
        [.. "unexport"u8],
        [.. "override"u8],
        [.. "define"u8],
        [.. "endef"u8],
        [.. "vpath"u8],
        [.. "private"u8],
        [.. "undefine"u8]);

    /// <summary>Built-in function names recognized inside <c>$(name ...)</c> calls.</summary>
    private static readonly ByteKeywordSet Builtins = ByteKeywordSet.Create(
        [.. "shell"u8],
        [.. "eval"u8],
        [.. "call"u8],
        [.. "if"u8],
        [.. "foreach"u8],
        [.. "filter"u8],
        [.. "filter-out"u8],
        [.. "patsubst"u8],
        [.. "subst"u8],
        [.. "wildcard"u8],
        [.. "addprefix"u8],
        [.. "addsuffix"u8],
        [.. "basename"u8],
        [.. "dir"u8],
        [.. "notdir"u8],
        [.. "suffix"u8],
        [.. "realpath"u8],
        [.. "abspath"u8],
        [.. "info"u8],
        [.. "warning"u8],
        [.. "error"u8],
        [.. "origin"u8],
        [.. "flavor"u8],
        [.. "value"u8],
        [.. "strip"u8],
        [.. "words"u8],
        [.. "word"u8],
        [.. "wordlist"u8],
        [.. "firstword"u8],
        [.. "lastword"u8],
        [.. "join"u8],
        [.. "sort"u8]);

    /// <summary>First-byte set for whitespace.</summary>
    private static readonly SearchValues<byte> WhitespaceFirst = TokenMatchers.AsciiWhitespaceWithNewlines;

    /// <summary>First-byte set for the hash-comment rule.</summary>
    private static readonly SearchValues<byte> HashFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for the variable-expansion rule (<c>$(...)</c>, <c>${...}</c>, <c>$x</c>).</summary>
    private static readonly SearchValues<byte> DollarFirst = SearchValues.Create("$"u8);

    /// <summary>Allowed single-byte automatic variables (<c>$@</c>, <c>$&lt;</c>, <c>$^</c>, <c>$*</c>, …).</summary>
    private static readonly SearchValues<byte> AutomaticVariableBytes = SearchValues.Create("@<^?+*%"u8);

    /// <summary>First-byte set for general keywords.</summary>
    private static readonly SearchValues<byte> KeywordFirst = SearchValues.Create("aeio"u8);

    /// <summary>First-byte set for declaration keywords; covers both the bare directive forms and the <c>-include</c> dash-prefixed form.</summary>
    private static readonly SearchValues<byte> KeywordDeclarationFirst = SearchValues.Create("-deopuvis"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationSet = SearchValues.Create("(){};,."u8);

    /// <summary>Operator alternation, sorted longest-first.</summary>
    private static readonly byte[][] OperatorTable =
    [
        [.. ":="u8],
        [.. "?="u8],
        [.. "+="u8],
        [.. "!="u8],
        [.. "=="u8],
        [.. "::"u8],
        [.. "="u8],
        [.. ":"u8],
        [.. "@"u8]
    ];

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("=:?+!@"u8);

    /// <summary>Identifier-continuation set — Makefile identifiers may contain <c>-</c> and <c>.</c>.</summary>
    private static readonly SearchValues<byte> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-."u8);

    /// <summary>Gets the singleton Makefile lexer.</summary>
    public static Lexer Instance { get; } = Build();

    /// <summary>Builds the Makefile lexer.</summary>
    /// <returns>Lexer.</returns>
    private static Lexer Build()
    {
        LexerRule[] rules =
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = WhitespaceFirst },
            new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },

            // $(VAR), ${VAR}, $@ / $< automatic-variable, $$ literal — must precede operator rule because $ would otherwise fall through.
            new(MatchVariableExpansion, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = DollarFirst },

            // "..." / '...' string literals.
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },
            new(static slice => TokenMatchers.MatchQuotedWithBackslashEscape(slice, (byte)'\''), TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },

            // Numbers.
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

            // Conditional directives — line-anchored so they only fire at start-of-line.
            new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = KeywordFirst, RequiresLineStart = true },

            // Module directives — line-anchored. Note `-include` starts with `-`.
            new(MatchDeclarationDirective, TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst, RequiresLineStart = true },

            // Built-in function names recognized everywhere (e.g. inside $(shell ...)).
            new(static slice => TokenMatchers.MatchKeyword(slice, Builtins), TokenClass.NameBuiltin, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

            new(
                static slice => TokenMatchers.MatchIdentifier(slice, TokenMatchers.AsciiIdentifierStart, IdentifierContinue),
                TokenClass.Name,
                LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },
            new(static slice => TokenMatchers.MatchLongestLiteral(slice, OperatorTable), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst },
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationSet), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationSet }
        ];

        return new(LanguageRuleBuilder.BuildSingleState(rules));
    }

    /// <summary>Matches Makefile variable expansion forms — <c>$(VAR)</c>, <c>${VAR}</c>, <c>$@</c>, <c>$&lt;</c>, <c>$$</c> literal.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchVariableExpansion(ReadOnlySpan<byte> slice)
    {
        const int DollarPlusOne = 2;
        if (slice.Length < DollarPlusOne || slice[0] is not (byte)'$')
        {
            return 0;
        }

        // $$ — literal dollar.
        if (slice[1] is (byte)'$')
        {
            return DollarPlusOne;
        }

        // $(...) — function call or variable.
        var paren = TokenMatchers.MatchPrefixedBracketedBlock(slice, (byte)'$', (byte)'(', (byte)')');
        if (paren > 0)
        {
            return paren;
        }

        // ${...} — variable expansion.
        var brace = TokenMatchers.MatchPrefixedBracketedBlock(slice, (byte)'$', (byte)'{', (byte)'}');
        if (brace > 0)
        {
            return brace;
        }

        // $@ / $< / $^ / $? automatic variables, or $X single-letter user variable.
        if (AutomaticVariableBytes.Contains(slice[1]) || TokenMatchers.AsciiIdentifierStart.Contains(slice[1]))
        {
            return DollarPlusOne;
        }

        return 0;
    }

    /// <summary>Matches a Makefile directive — either a bare keyword or the <c>-include</c> dash-prefixed form.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchDeclarationDirective(ReadOnlySpan<byte> slice)
    {
        const int IncludeLength = 7;
        const int DashIncludeLength = 8;
        if (slice is [(byte)'-', ..]
            && slice[1..].StartsWith("include"u8)
            && (slice.Length == DashIncludeLength
                || !TokenMatchers.AsciiIdentifierContinue.Contains(slice[1 + IncludeLength])))
        {
            return DashIncludeLength;
        }

        return slice is [(byte)'-', ..] ? 0 : TokenMatchers.MatchKeyword(slice, KeywordDeclarations);
    }
}
