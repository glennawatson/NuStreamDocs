// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Per-language configuration consumed by <see cref="IniFamilyRules.Build"/>.</summary>
/// <remarks>
/// Populated via object-initializer syntax so a new dialect (INI, TOML, .properties,
/// .editorconfig, systemd unit files) is one set of toggles + a comment-first-byte
/// table away from a working lexer.
/// </remarks>
internal readonly record struct IniFamilyConfig
{
    /// <summary>Gets the first-byte set for line comments (typically <c>;#</c>, <c>#</c>, or <c>#!</c>).</summary>
    public SearchValues<byte> CommentFirst { get; init; }

    /// <summary>Gets the first-byte set for the key-value separator (typically <c>=</c> or <c>=:</c>).</summary>
    public SearchValues<byte> SeparatorFirst { get; init; }

    /// <summary>Gets a value indicating whether <c>[[double-bracket]]</c> headers (TOML array-of-tables) are recognized in addition to <c>[single-bracket]</c>.</summary>
    public bool RecognizeDoubleBracketHeader { get; init; }

    /// <summary>Gets a value indicating whether double-quoted and single-quoted string literals classify as strings on the value side.</summary>
    public bool RecognizeStringLiterals { get; init; }

    /// <summary>Gets a value indicating whether numeric literals (integers, floats with optional <c>_</c> separators) classify as numbers.</summary>
    public bool RecognizeNumericLiterals { get; init; }

    /// <summary>Gets the optional boolean / constant keyword set (case-sensitive); <see langword="null"/> disables the rule.</summary>
    public ByteKeywordSet? KeywordConstants { get; init; }

    /// <summary>Gets the first-byte dispatch set for <see cref="KeywordConstants"/>; only meaningful when <see cref="KeywordConstants"/> is non-null.</summary>
    public SearchValues<byte>? KeywordConstantFirst { get; init; }
}
