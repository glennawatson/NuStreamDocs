// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Shared keyword / first-byte tables for the ML-family lexers (Haskell, OCaml, F#, Elm, ReasonML).</summary>
internal static class MlFamilyShared
{
    /// <summary>First-byte dispatch set covering every common-keyword leading byte.</summary>
    public static readonly SearchValues<byte> CommonKeywordFirst = SearchValues.Create("aceilotw"u8);

    /// <summary>Gets the common control-flow / pattern-match keywords every ML-family dialect ships with, as a space-separated literal.</summary>
    public static ReadOnlySpan<byte> CommonKeywordsLiteral => "if then else case of let in as where"u8;
}
