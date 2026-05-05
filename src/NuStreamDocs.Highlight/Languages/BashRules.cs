// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>
/// Reusable Bash / sh / zsh rule list factory. Extracted as a helper
/// so future shell-embedding lexers (Dockerfile, GitHub Actions
/// shell-step blocks) classify shell tokens identically.
/// </summary>
internal static class BashRules
{
    /// <summary>Shell keywords recognized as <see cref="TokenClass.Keyword"/>.</summary>
    private static readonly ByteKeywordSet Keywords = ByteKeywordSet.Create(
        [.. "if"u8],
        [.. "then"u8],
        [.. "else"u8],
        [.. "elif"u8],
        [.. "fi"u8],
        [.. "case"u8],
        [.. "esac"u8],
        [.. "for"u8],
        [.. "select"u8],
        [.. "while"u8],
        [.. "until"u8],
        [.. "do"u8],
        [.. "done"u8],
        [.. "in"u8],
        [.. "function"u8],
        [.. "return"u8],
        [.. "break"u8],
        [.. "continue"u8],
        [.. "exit"u8],
        [.. "export"u8],
        [.. "local"u8],
        [.. "readonly"u8],
        [.. "declare"u8],
        [.. "typeset"u8],
        [.. "unset"u8],
        [.. "shift"u8],
        [.. "trap"u8],
        [.. "wait"u8],
        [.. "exec"u8],
        [.. "eval"u8],
        [.. "set"u8],
        [.. "test"u8]);

    /// <summary>Common shell builtins recognized as <see cref="TokenClass.NameBuiltin"/>.</summary>
    private static readonly ByteKeywordSet Builtins = ByteKeywordSet.Create(
        [.. "echo"u8],
        [.. "printf"u8],
        [.. "read"u8],
        [.. "cd"u8],
        [.. "pwd"u8],
        [.. "pushd"u8],
        [.. "popd"u8],
        [.. "dirs"u8],
        [.. "let"u8],
        [.. "alias"u8],
        [.. "unalias"u8],
        [.. "source"u8],
        [.. "history"u8],
        [.. "jobs"u8],
        [.. "kill"u8],
        [.. "fg"u8],
        [.. "bg"u8],
        [.. "umask"u8],
        [.. "true"u8],
        [.. "false"u8],
        [.. "type"u8],
        [.. "command"u8],
        [.. "builtin"u8],
        [.. "hash"u8],
        [.. "times"u8],
        [.. "getopts"u8]);

    /// <summary>Operators sorted by descending length so longer alternations win against shorter prefixes.</summary>
    private static readonly byte[][] Operators =
    [
        [.. "&&"u8], [.. "||"u8], [.. "<<"u8], [.. ">>"u8],
        [.. "<="u8], [.. ">="u8], [.. "=="u8], [.. "!="u8], [.. "=~"u8],
        [.. "<"u8], [.. ">"u8], [.. "+"u8], [.. "-"u8], [.. "*"u8], [.. "/"u8],
        [.. "%"u8], [.. "&"u8], [.. "|"u8], [.. "!"u8], [.. "~"u8], [.. "="u8], [.. "?"u8]
    ];

    /// <summary>First-byte set for <c>#</c>-prefixed comments.</summary>
    private static readonly SearchValues<byte> CommentFirst = SearchValues.Create("#"u8);

    /// <summary>First-byte set for variable substitutions (<c>$name</c>, <c>${name}</c>).</summary>
    private static readonly SearchValues<byte> DollarFirst = SearchValues.Create("$"u8);

    /// <summary>Single special-byte names allowed after a bare <c>$</c> (<c>$1</c>, <c>$@</c>, <c>$?</c>, …).</summary>
    private static readonly SearchValues<byte> DollarSpecial = SearchValues.Create("0123456789#?!_*@-"u8);

    /// <summary>First-byte set for shell operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("&|<>=!+-*/%~?"u8);

    /// <summary>First-byte set for structural punctuation.</summary>
    private static readonly SearchValues<byte> PunctuationFirst = SearchValues.Create("(){}[];,.:"u8);

    /// <summary>
    /// Builds and returns an array of lexer rules for parsing Bash syntax.
    /// Each rule defines a pattern to match, the token class associated with the match,
    /// and state transitions required during lexical analysis.
    /// </summary>
    /// <returns>An array of <c>LexerRule</c> objects describing the syntax rules for Bash tokenization.</returns>
    public static LexerRule[] Build() =>
    [

        // [ \t\r\n]+ whitespace runs.
        new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiWhitespaceWithNewlines },

        // # line comment to end-of-line.
        new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, LexerRule.NoStateChange) { FirstBytes = CommentFirst },

        // '...' single-quoted string (no escapes).
        new(TokenMatchers.MatchSingleQuotedNoEscape, TokenClass.StringSingle, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.SingleQuoteFirst },

        // "..." double-quoted string with backslash escapes.
        new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, LexerRule.NoStateChange) { FirstBytes = LanguageCommon.DoubleQuoteFirst },

        // [0-9]+ integer literal.
        new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiDigits },

        // ${...} braced variable substitution — must precede the simple-variable rule.
        new(MatchBracedVariable, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = DollarFirst },

        // $name or $1 / $@ / $? simple variable.
        new(MatchSimpleVariable, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = DollarFirst },

        // Shell keyword (if, then, else, for, while, ...).
        new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

        // Shell builtin (echo, printf, cd, ...).
        new(static slice => TokenMatchers.MatchKeyword(slice, Builtins), TokenClass.NameBuiltin, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

        // [A-Za-z_][A-Za-z0-9_]* identifier — falls through after keyword + builtin.
        new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, LexerRule.NoStateChange) { FirstBytes = TokenMatchers.AsciiIdentifierStart },

        // Operator alternation (longest-first): &&, ||, <<, >>, <=, >=, ==, !=, =~, single bytes.
        new(static slice => TokenMatchers.MatchLongestLiteral(slice, Operators), TokenClass.Operator, LexerRule.NoStateChange) { FirstBytes = OperatorFirst },

        // Single-byte punctuation: ( ) { } [ ] ; , . :
        new(static slice => TokenMatchers.MatchSingleByteOf(slice, PunctuationFirst), TokenClass.Punctuation, LexerRule.NoStateChange) { FirstBytes = PunctuationFirst }
    ];

    /// <summary><c>${...}</c> braced variable substitution.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchBracedVariable(ReadOnlySpan<byte> slice)
    {
        if (slice is [] || slice[0] is not (byte)'$')
        {
            return 0;
        }

        var bracket = TokenMatchers.MatchBracketedBlock(slice[1..], (byte)'{', (byte)'}');
        return bracket is 0 ? 0 : 1 + bracket;
    }

    /// <summary><c>$identifier</c> or <c>$specialChar</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchSimpleVariable(ReadOnlySpan<byte> slice)
    {
        const int DollarPlusOne = 2;
        if (slice.Length < DollarPlusOne || slice[0] is not (byte)'$')
        {
            return 0;
        }

        var ident = TokenMatchers.MatchAsciiIdentifier(slice[1..]);
        if (ident > 0)
        {
            return 1 + ident;
        }

        return DollarSpecial.Contains(slice[1]) ? DollarPlusOne : 0;
    }
}
