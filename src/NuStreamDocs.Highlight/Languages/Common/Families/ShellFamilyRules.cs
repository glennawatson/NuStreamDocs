// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common.Builders;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Reusable shell-family lexer rule builder.</summary>
/// <remarks>
/// Single-state lexer covering <c>#</c> line comments, single-quoted no-escape
/// strings, double-quoted backslash-escape strings, <c>$name</c> / <c>${name}</c>
/// variable substitutions, integer literals, keyword / builtin classification,
/// and the operator + punctuation tail. Bash, sh, and zsh all share this shape;
/// future Fish / Tcsh / Cmd / Perl-heredoc lexers slot in here too.
/// </remarks>
internal static class ShellFamilyRules
{
    /// <summary>First-byte set for hash-prefixed comments.</summary>
    public static readonly SearchValues<byte> CommentFirst = SearchValues.Create("#"u8);

    /// <summary>Single-byte structural punctuation.</summary>
    public static readonly SearchValues<byte> PunctuationFirst = SearchValues.Create("(){}[];,.:"u8);

    /// <summary>Builds the shell-family ordered rule list from <paramref name="config"/>.</summary>
    /// <param name="config">Per-language configuration.</param>
    /// <returns>Ordered <see cref="LexerRule"/> list for the root state.</returns>
    public static LexerRule[] Build(in ShellFamilyConfig config)
    {
        byte[] sigilArray = [config.VariableSigil];
        var sigilFirst = SearchValues.Create(sigilArray.AsSpan(0, 1));
        var specialBytes = config.SpecialVariableBytes;
        var sigil = config.VariableSigil;
        var keywords = config.Keywords;
        var builtins = config.Builtins;
        var operators = config.Operators;
        var opFirst = config.OperatorFirst;

        return
        [
            new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiWhitespaceWithNewlines },

            // # line comment to end-of-line.
            new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = CommentFirst },

            // '...' single-quoted (no escapes).
            new(TokenMatchers.MatchSingleQuotedNoEscape, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },

            // "..." double-quoted with backslash escapes.
            new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

            // [0-9]+ integer literal.
            new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

            // ${name} braced variable — must precede the simple-variable rule.
            new(slice => MatchBracedVariable(slice, sigil), TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = sigilFirst },

            // $name or $1 / $@ / $? simple variable.
            new(slice => MatchSimpleVariable(slice, sigil, specialBytes), TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = sigilFirst },

            // Shell keyword (if, then, else, ...).
            new(slice => TokenMatchers.MatchKeyword(slice, keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

            // Shell builtin (echo, printf, cd, ...).
            new(slice => TokenMatchers.MatchKeyword(slice, builtins), TokenClass.NameBuiltin, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

            // [A-Za-z_][A-Za-z0-9_]* identifier.
            new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

            // Operator alternation, longest-first.
            new(slice => TokenMatchers.MatchLongestLiteral(slice, operators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = opFirst },

            // Single-byte structural punctuation.
            new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationFirst), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationFirst }
        ];
    }

    /// <summary>Matches a <c>${name}</c> / <c>${expr}</c> braced variable substitution.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="sigil">Variable-substitution sigil byte.</param>
    /// <returns>Length matched, or zero.</returns>
    private static int MatchBracedVariable(ReadOnlySpan<byte> slice, byte sigil)
    {
        if (slice is [] || slice[0] != sigil)
        {
            return 0;
        }

        var bracket = TokenMatchers.MatchBracketedBlock(slice[1..], (byte)'{', (byte)'}');
        return bracket is 0 ? 0 : 1 + bracket;
    }

    /// <summary>Matches a <c>$name</c> identifier or <c>$specialByte</c> single-byte variable.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <param name="sigil">Variable-substitution sigil byte.</param>
    /// <param name="specialBytes">Allowed single-byte special variable bytes.</param>
    /// <returns>Length matched (sigil + body), or zero.</returns>
    private static int MatchSimpleVariable(ReadOnlySpan<byte> slice, byte sigil, SearchValues<byte> specialBytes)
    {
        const int SigilPlusOne = 2;
        if (slice.Length < SigilPlusOne || slice[0] != sigil)
        {
            return 0;
        }

        var ident = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        if (ident > 0)
        {
            return 1 + ident;
        }

        return specialBytes.Contains(slice[1]) ? SigilPlusOne : 0;
    }
}
