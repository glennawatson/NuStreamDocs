// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

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

    /// <summary>Gets the general-keyword set.</summary>
    public ByteKeywordSet Keywords { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for general keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordFirst { get; init; }

    /// <summary>Gets the type-keyword set.</summary>
    public ByteKeywordSet KeywordTypes { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for type keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordTypeFirst { get; init; }

    /// <summary>Gets the declaration-keyword set.</summary>
    public ByteKeywordSet KeywordDeclarations { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for declaration keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordDeclarationFirst { get; init; }

    /// <summary>Gets the constant-keyword set (booleans, unit, none-style sentinels).</summary>
    public ByteKeywordSet KeywordConstants { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for constant keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordConstantFirst { get; init; }

    /// <summary>Gets the operator alternation, sorted longest-first.</summary>
    public byte[][] Operators { get; init; }

    /// <summary>
    /// Gets the optional first-byte dispatch set for operators;
    /// <see langword="null"/> falls back to <see cref="OperatorAlternationFactory.FirstBytesOf"/> over <see cref="Operators"/>.
    /// </summary>
    public SearchValues<byte>? OperatorFirst { get; init; }
}
