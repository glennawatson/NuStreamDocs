// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Per-language configuration consumed by <see cref="ShellFamilyRules.Build"/>.</summary>
/// <remarks>
/// Captures the dialect-specific shape: keyword set, builtin set, operator table,
/// the variable sigil byte (typically <c>$</c>), and the byte set of special
/// single-byte variable names (<c>$1</c>, <c>$@</c>, <c>$?</c>, …).
/// </remarks>
internal readonly record struct ShellFamilyConfig
{
    /// <summary>Gets the shell-keyword set (<c>if</c>, <c>then</c>, <c>case</c>, <c>do</c>, <c>function</c>, …).</summary>
    public ByteKeywordSet Keywords { get; init; }

    /// <summary>Gets the shell-builtin set (<c>echo</c>, <c>printf</c>, <c>cd</c>, …).</summary>
    public ByteKeywordSet Builtins { get; init; }

    /// <summary>Gets the operator alternation, sorted longest-first.</summary>
    public byte[][] Operators { get; init; }

    /// <summary>Gets the first-byte dispatch set for operators.</summary>
    public SearchValues<byte> OperatorFirst { get; init; }

    /// <summary>Gets the variable-substitution sigil (<c>$</c> for Bash; <c>%</c> for cmd would slot in here).</summary>
    public byte VariableSigil { get; init; }

    /// <summary>Gets the byte set of allowed single-byte special variable names (<c>$1</c>, <c>$@</c>, <c>$?</c>, …).</summary>
    public SearchValues<byte> SpecialVariableBytes { get; init; }
}
