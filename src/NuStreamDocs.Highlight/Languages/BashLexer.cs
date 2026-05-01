// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Bash / sh / zsh lexer.</summary>
/// <remarks>
/// Pragmatic subset of Pygments' <c>BashLexer</c>: comments, single
/// and double-quoted strings, variable substitution (<c>$name</c>,
/// <c>${name}</c>, <c>$(...)</c>), shell keywords, numbers, and
/// operators. Heredocs and process substitution are out of scope for
/// the first cut.
/// </remarks>
public static partial class BashLexer
{
    /// <summary>Pattern alternation for shell keywords.</summary>
    private const string Keywords =
        "if|then|else|elif|fi|case|esac|for|select|while|until|do|done|in|" +
        "function|return|break|continue|exit|export|local|readonly|declare|" +
        "typeset|unset|shift|trap|wait|exec|eval|set|test";

    /// <summary>Pattern alternation for common shell builtins.</summary>
    private const string Builtins =
        "echo|printf|read|cd|pwd|pushd|popd|dirs|let|alias|unalias|" +
        "source|history|jobs|kill|fg|bg|umask|true|false|type|command|" +
        "builtin|hash|times|getopts";

    /// <summary>First-char set for whitespace runs.</summary>
    private static readonly SearchValues<char> WhitespaceFirst = SearchValues.Create(" \t\r\n");

    /// <summary>First-char set for <c>#</c>-prefixed comments.</summary>
    private static readonly SearchValues<char> CommentFirst = SearchValues.Create("#");

    /// <summary>First-char set for single-quoted string literals.</summary>
    private static readonly SearchValues<char> SingleQuoteFirst = SearchValues.Create("'");

    /// <summary>First-char set for double-quoted string literals.</summary>
    private static readonly SearchValues<char> DoubleQuoteFirst = SearchValues.Create("\"");

    /// <summary>First-char set for numeric literals.</summary>
    private static readonly SearchValues<char> DigitFirst = SearchValues.Create("0123456789");

    /// <summary>First-char set for variable substitutions (<c>$name</c>, <c>${name}</c>).</summary>
    private static readonly SearchValues<char> DollarFirst = SearchValues.Create("$");

    /// <summary>First-char set for identifier-shaped tokens (keywords, builtins, names).</summary>
    private static readonly SearchValues<char> IdentifierFirst = SearchValues.Create(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_");

    /// <summary>First-char set for shell operators.</summary>
    private static readonly SearchValues<char> OperatorFirst = SearchValues.Create("&|<>=!+-*/%~?");

    /// <summary>First-char set for structural punctuation.</summary>
    private static readonly SearchValues<char> PunctuationFirst = SearchValues.Create("(){}[];,.:");

    /// <summary>Gets the singleton lexer instance.</summary>
    public static Lexer Instance { get; } = new(
        "bash",
        new Dictionary<string, LexerRule[]>(StringComparer.Ordinal)
        {
            [Lexer.RootState] =
            [
                new(WhitespaceRegex(), TokenClass.Whitespace, NextState: null) { FirstChars = WhitespaceFirst },
                new(CommentRegex(), TokenClass.CommentSingle, NextState: null) { FirstChars = CommentFirst },
                new(SingleStringRegex(), TokenClass.StringSingle, NextState: null) { FirstChars = SingleQuoteFirst },
                new(DoubleStringRegex(), TokenClass.StringDouble, NextState: null) { FirstChars = DoubleQuoteFirst },
                new(NumberRegex(), TokenClass.NumberInteger, NextState: null) { FirstChars = DigitFirst },
                new(VariableBracedRegex(), TokenClass.Name, NextState: null) { FirstChars = DollarFirst },
                new(VariableSimpleRegex(), TokenClass.Name, NextState: null) { FirstChars = DollarFirst },
                new(KeywordRegex(), TokenClass.Keyword, NextState: null) { FirstChars = IdentifierFirst },
                new(BuiltinRegex(), TokenClass.NameBuiltin, NextState: null) { FirstChars = IdentifierFirst },
                new(IdentifierRegex(), TokenClass.Name, NextState: null) { FirstChars = IdentifierFirst },
                new(OperatorRegex(), TokenClass.Operator, NextState: null) { FirstChars = OperatorFirst },
                new(PunctuationRegex(), TokenClass.Punctuation, NextState: null) { FirstChars = PunctuationFirst },
            ],
        }.ToFrozenDictionary(StringComparer.Ordinal));

    [GeneratedRegex(@"\G[ \t\r\n]+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\G\#[^\r\n]*", RegexOptions.Compiled)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"\G'[^']*'", RegexOptions.Compiled)]
    private static partial Regex SingleStringRegex();

    [GeneratedRegex("\\G\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.Compiled)]
    private static partial Regex DoubleStringRegex();

    [GeneratedRegex(@"\G[0-9]+", RegexOptions.Compiled)]
    private static partial Regex NumberRegex();

    [GeneratedRegex(@"\G\$\{[^}]*\}", RegexOptions.Compiled)]
    private static partial Regex VariableBracedRegex();

    [GeneratedRegex(@"\G\$[A-Za-z_][A-Za-z0-9_]*|\$[\d#?!_*@-]", RegexOptions.Compiled)]
    private static partial Regex VariableSimpleRegex();

    [GeneratedRegex(@"\G(?:" + Keywords + @")\b", RegexOptions.Compiled)]
    private static partial Regex KeywordRegex();

    [GeneratedRegex(@"\G(?:" + Builtins + @")\b", RegexOptions.Compiled)]
    private static partial Regex BuiltinRegex();

    [GeneratedRegex(@"\G[A-Za-z_][A-Za-z0-9_]*", RegexOptions.Compiled)]
    private static partial Regex IdentifierRegex();

    [GeneratedRegex(@"\G(?:&&|\|\||<<|>>|<=|>=|==|!=|=~|<|>|\+|-|\*|/|%|&|\||!|~|=|\?)", RegexOptions.Compiled)]
    private static partial Regex OperatorRegex();

    [GeneratedRegex(@"\G[\(\)\{\}\[\];,.:]", RegexOptions.Compiled)]
    private static partial Regex PunctuationRegex();
}
