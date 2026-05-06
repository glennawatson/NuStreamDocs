// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Per-language configuration consumed by <see cref="MlFamilyRules.Build"/>.</summary>
/// <remarks>
/// Captures the dialect-specific shape: block-comment delimiter pair, optional line-comment prefix,
/// and the keyword sets. The block-comment matcher is nesting-aware (<see cref="MlFamilyRules"/>
/// implements OCaml / Haskell semantics) — set <see cref="BlockCommentOpen"/> and
/// <see cref="BlockCommentClose"/> to the dialect's two-byte delimiters.
/// </remarks>
internal readonly record struct MlFamilyConfig
{
    /// <summary>Gets the two-byte block-comment opener (e.g. <c>"(*"u8</c> for OCaml / F#, <c>"{-"u8</c> for Haskell).</summary>
    public byte[] BlockCommentOpen { get; init; }

    /// <summary>Gets the two-byte block-comment closer (e.g. <c>"*)"u8</c> / <c>"-}"u8</c>).</summary>
    public byte[] BlockCommentClose { get; init; }

    /// <summary>Gets the optional line-comment prefix (e.g. <c>"--"u8</c> for Haskell); <see langword="null"/> disables the rule.</summary>
    public byte[]? LineCommentPrefix { get; init; }

    /// <summary>Gets the keyword + operator table bundle.</summary>
    public KeywordTablePack Tables { get; init; }
}
