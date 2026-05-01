// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Bash / sh / zsh lexer.</summary>
/// <remarks>
/// Pragmatic subset of Pygments' <c>BashLexer</c>: comments, single
/// and double-quoted strings, variable substitution (<c>$name</c>,
/// <c>${name}</c>, <c>$(...)</c>), shell keywords, numbers, and
/// operators. Heredocs and process substitution are out of scope for
/// the first cut.
/// </remarks>
public static class BashLexer
{
    /// <summary>Shell keywords recognised as <see cref="TokenClass.Keyword"/>.</summary>
    private static readonly FrozenSet<string> Keywords = FrozenSet.ToFrozenSet(
        [
            "if", "then", "else", "elif", "fi", "case", "esac", "for", "select",
            "while", "until", "do", "done", "in", "function", "return", "break",
            "continue", "exit", "export", "local", "readonly", "declare", "typeset",
            "unset", "shift", "trap", "wait", "exec", "eval", "set", "test",
        ],
        StringComparer.Ordinal);

    /// <summary>Common shell builtins recognised as <see cref="TokenClass.NameBuiltin"/>.</summary>
    private static readonly FrozenSet<string> Builtins = FrozenSet.ToFrozenSet(
        [
            "echo", "printf", "read", "cd", "pwd", "pushd", "popd", "dirs", "let",
            "alias", "unalias", "source", "history", "jobs", "kill", "fg", "bg",
            "umask", "true", "false", "type", "command", "builtin", "hash", "times",
            "getopts",
        ],
        StringComparer.Ordinal);

    /// <summary>Operators sorted by descending length so longer alternations win against shorter prefixes.</summary>
    private static readonly string[] Operators =
    [
        "&&", "||", "<<", ">>", "<=", ">=", "==", "!=", "=~",
        "<", ">", "+", "-", "*", "/", "%", "&", "|", "!", "~", "=", "?",
    ];

    /// <summary>First-char set for <c>#</c>-prefixed comments.</summary>
    private static readonly SearchValues<char> CommentFirst = SearchValues.Create("#");

    /// <summary>First-char set for variable substitutions (<c>$name</c>, <c>${name}</c>).</summary>
    private static readonly SearchValues<char> DollarFirst = SearchValues.Create("$");

    /// <summary>Single special-character names allowed after a bare <c>$</c> (<c>$1</c>, <c>$@</c>, <c>$?</c>, …).</summary>
    private static readonly SearchValues<char> DollarSpecial = SearchValues.Create("0123456789#?!_*@-");

    /// <summary>First-char set for shell operators.</summary>
    private static readonly SearchValues<char> OperatorFirst = SearchValues.Create("&|<>=!+-*/%~?");

    /// <summary>First-char set for structural punctuation.</summary>
    private static readonly SearchValues<char> PunctuationFirst = SearchValues.Create("(){}[];,.:");

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        "bash",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] = [

                // [ \t\r\n]+ whitespace runs.
                new(TokenMatchers.MatchAsciiWhitespace, TokenClass.Whitespace, NextState: null) { FirstChars = TokenMatchers.AsciiWhitespaceWithNewlines },

                // # line comment to end-of-line.
                new(TokenMatchers.MatchHashComment, TokenClass.CommentSingle, NextState: null) { FirstChars = CommentFirst },

                // '...' single-quoted string (no escapes).
                new(TokenMatchers.MatchSingleQuotedNoEscape, TokenClass.StringSingle, NextState: null) { FirstChars = LanguageCommon.SingleQuoteFirst },

                // "..." double-quoted string with backslash escapes.
                new(TokenMatchers.MatchDoubleQuotedWithBackslashEscape, TokenClass.StringDouble, NextState: null) { FirstChars = LanguageCommon.DoubleQuoteFirst },

                // [0-9]+ integer literal.
                new(TokenMatchers.MatchAsciiDigits, TokenClass.NumberInteger, NextState: null) { FirstChars = TokenMatchers.AsciiDigits },

                // ${...} braced variable substitution — must precede the simple-variable rule.
                new(MatchBracedVariable, TokenClass.Name, NextState: null) { FirstChars = DollarFirst },

                // $name or $1 / $@ / $? simple variable.
                new(MatchSimpleVariable, TokenClass.Name, NextState: null) { FirstChars = DollarFirst },

                // Shell keyword (if, then, else, for, while, ...).
                new(static slice => TokenMatchers.MatchKeyword(slice, Keywords), TokenClass.Keyword, NextState: null) { FirstChars = TokenMatchers.AsciiIdentifierStart },

                // Shell builtin (echo, printf, cd, ...).
                new(static slice => TokenMatchers.MatchKeyword(slice, Builtins), TokenClass.NameBuiltin, NextState: null) { FirstChars = TokenMatchers.AsciiIdentifierStart },

                // [A-Za-z_][A-Za-z0-9_]* identifier — falls through after keyword + builtin.
                new(TokenMatchers.MatchAsciiIdentifier, TokenClass.Name, NextState: null) { FirstChars = TokenMatchers.AsciiIdentifierStart },

                // Operator alternation (longest-first): &&, ||, <<, >>, <=, >=, ==, !=, =~, single chars.
                new(static slice => TokenMatchers.MatchLongestLiteral(slice, Operators), TokenClass.Operator, NextState: null) { FirstChars = OperatorFirst },

                // Single-character punctuation: ( ) { } [ ] ; , . :
                new(static slice => TokenMatchers.MatchSingleCharOf(slice, PunctuationFirst), TokenClass.Punctuation, NextState: null) { FirstChars = PunctuationFirst },
            ],
        }.ToFrozenDictionary(StringComparer.Ordinal));

    /// <summary><c>${...}</c> braced variable substitution.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchBracedVariable(ReadOnlySpan<char> slice)
    {
        if (slice is [] || slice[0] is not '$')
        {
            return 0;
        }

        var bracket = TokenMatchers.MatchBracketedBlock(slice[1..], '{', '}');
        return bracket is 0 ? 0 : 1 + bracket;
    }

    /// <summary><c>$identifier</c> or <c>$specialChar</c>.</summary>
    /// <param name="slice">Slice anchored at the cursor.</param>
    /// <returns>Length matched.</returns>
    private static int MatchSimpleVariable(ReadOnlySpan<char> slice)
    {
        const int DollarPlusOne = 2;
        if (slice.Length < DollarPlusOne || slice[0] is not '$')
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
