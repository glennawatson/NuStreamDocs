// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common;

/// <summary>Shared keyword / first-byte tables for the ML-family lexers (Haskell, OCaml, F#, Elm, ReasonML).</summary>
/// <remarks>
/// Spread these byte-array tables through a collection expression
/// (<c>[.. MlFamilyShared.CommonKeywords, [.. "do"u8]]</c>) when building a per-language
/// <see cref="ByteKeywordSet"/> so the duplicated <c>if</c>/<c>then</c>/<c>else</c>/… entries
/// only appear once across the project.
/// </remarks>
internal static class MlFamilyShared
{
    /// <summary>Common control-flow / pattern-match keywords every ML-family dialect ships with.</summary>
    public static readonly byte[][] CommonKeywords =
    [
        [.. "if"u8],
        [.. "then"u8],
        [.. "else"u8],
        [.. "case"u8],
        [.. "of"u8],
        [.. "let"u8],
        [.. "in"u8],
        [.. "as"u8],
        [.. "where"u8]
    ];

    /// <summary>
    /// First-byte dispatch set covering every byte that any <see cref="CommonKeywords"/>
    /// entry starts with (<c>a</c>, <c>c</c>, <c>e</c>, <c>i</c>, <c>l</c>, <c>o</c>,
    /// <c>t</c>, <c>w</c>).
    /// </summary>
    public static readonly SearchValues<byte> CommonKeywordFirst = SearchValues.Create("aceilotw"u8);
}
