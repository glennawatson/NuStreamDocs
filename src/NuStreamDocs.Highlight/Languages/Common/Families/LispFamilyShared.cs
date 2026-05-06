// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Shared keyword / first-byte tables for the Lisp-family lexers (Common Lisp, Scheme, Racket, Clojure, Emacs Lisp).</summary>
/// <remarks>
/// Spread these byte-array tables through a collection expression so the recurring
/// <c>if</c>/<c>cond</c>/<c>let</c>/… entries land in only one place in the source.
/// </remarks>
internal static class LispFamilyShared
{
    /// <summary>Common control-flow / binding forms shared across Lisp dialects.</summary>
    public static readonly byte[][] CommonKeywords =
    [
        [.. "if"u8],
        [.. "when"u8],
        [.. "unless"u8],
        [.. "cond"u8],
        [.. "case"u8],
        [.. "and"u8],
        [.. "or"u8],
        [.. "not"u8],
        [.. "do"u8],
        [.. "let"u8],
        [.. "lambda"u8],
        [.. "quote"u8]
    ];

    /// <summary>First-byte dispatch set for <see cref="CommonKeywords"/>.</summary>
    public static readonly SearchValues<byte> CommonKeywordFirst = SearchValues.Create("acdilnoquw"u8);

    /// <summary>Gets the common control-flow / binding forms shared across Lisp dialects, as a space-separated literal.</summary>
    public static ReadOnlySpan<byte> CommonKeywordsLiteral => "if when unless cond case and or not do let lambda quote"u8;
}
