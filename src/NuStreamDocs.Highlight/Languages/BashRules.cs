// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Highlight.Languages.Common;

namespace NuStreamDocs.Highlight.Languages;

/// <summary>Reusable Bash / sh / zsh rule list factory.</summary>
/// <remarks>
/// Delegates to <see cref="ShellFamilyRules"/>; per-Bash specifics are the
/// keyword / builtin / operator tables and the <c>$</c> sigil + special-variable
/// byte set. Future shell-embedding lexers (Dockerfile, GitHub Actions
/// shell-step blocks) classify shell tokens identically by reusing this rule list.
/// </remarks>
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

    /// <summary>Operators sorted longest-first.</summary>
    private static readonly byte[][] Operators =
    [
        [.. "&&"u8],
        [.. "||"u8],
        [.. "<<"u8],
        [.. ">>"u8],
        [.. "<="u8],
        [.. ">="u8],
        [.. "=="u8],
        [.. "!="u8],
        [.. "=~"u8],
        [.. "<"u8],
        [.. ">"u8],
        [.. "+"u8],
        [.. "-"u8],
        [.. "*"u8],
        [.. "/"u8],
        [.. "%"u8],
        [.. "&"u8],
        [.. "|"u8],
        [.. "!"u8],
        [.. "~"u8],
        [.. "="u8],
        [.. "?"u8]
    ];

    /// <summary>First-byte set for shell operators.</summary>
    private static readonly SearchValues<byte> OperatorFirst = SearchValues.Create("&|<>=!+-*/%~?"u8);

    /// <summary>Single special-byte names allowed after a bare <c>$</c> (<c>$1</c>, <c>$@</c>, <c>$?</c>, …).</summary>
    private static readonly SearchValues<byte> SpecialVariableBytes = SearchValues.Create("0123456789#?!_*@-"u8);

    /// <summary>Builds the Bash rule list.</summary>
    /// <returns>Ordered rule list.</returns>
    public static LexerRule[] Build()
    {
        ShellFamilyConfig config = new()
        {
            Keywords = Keywords,
            Builtins = Builtins,
            Operators = Operators,
            OperatorFirst = OperatorFirst,
            VariableSigil = (byte)'$',
            SpecialVariableBytes = SpecialVariableBytes
        };

        return ShellFamilyRules.Build(config);
    }
}
