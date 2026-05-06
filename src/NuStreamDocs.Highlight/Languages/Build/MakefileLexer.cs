// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Build;

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
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.CreateFromSpaceSeparated(
        "ifeq ifneq ifdef ifndef else endif if or and"u8);

    /// <summary>Declaration / module directives. The <c>-include</c> form is matched separately because <c>MatchKeyword</c> requires an ASCII-identifier start byte.</summary>
    private static readonly ByteKeywordSet KeywordDeclarations = ByteKeywordSet.CreateFromSpaceSeparated(
        "include sinclude export unexport override define endef vpath private undefine"u8);

    /// <summary>Built-in function names recognized inside <c>$(name ...)</c> calls.</summary>
    private static readonly ByteKeywordSet Builtins = ByteKeywordSet.CreateFromSpaceSeparated(
        "shell eval call if foreach filter filter-out patsubst subst wildcard addprefix addsuffix basename dir notdir suffix"u8,
        "realpath abspath info warning error origin flavor value strip words word wordlist firstword lastword join sort"u8);

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
    private static readonly byte[][] OperatorTable = OperatorAlternationFactory.SplitLongestFirst(
        ":= ?= += != == :: = : @"u8);

    /// <summary>First-byte set for operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("=:?+!@"u8);

    /// <summary>Identifier-continuation set — Makefile identifiers may contain <c>-</c> and <c>.</c>.</summary>
    private static readonly SearchValues<byte> IdentifierContinue = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-."u8);

    /// <summary>Gets the singleton Makefile lexer.</summary>
    public static Lexer Instance { get; } = SingleStateLexerRules.CreateLexer(new()
    {
        WhitespaceFirst = WhitespaceFirst,
        LineComment = new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = HashFirst },
        PostStringRules = [new(MatchVariableExpansion, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = DollarFirst }],
        IncludeDoubleQuotedString = true,
        IncludeSingleQuotedString = true,
        IncludeIntegerLiteral = true,
        Keywords = Keywords,
        KeywordFirst = KeywordFirst,
        KeywordsRequireLineStart = true,
        ExtraRules =
        [
            new(MatchDeclarationDirective, TokenClass.KeywordDeclaration, LexerRule.NoStateChange) { FirstBytes = KeywordDeclarationFirst, RequiresLineStart = true }
        ],
        BuiltinKeywords = Builtins,
        BuiltinKeywordFirst = TokenMatchers.AsciiIdentifierStart,
        IdentifierContinue = IdentifierContinue,
        Operators = OperatorTable,
        OperatorFirst = OperatorFirst,
        Punctuation = PunctuationSet
    });

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
