// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Highlight.Languages.Common.Families;

/// <summary>Per-language configuration consumed by <see cref="LispFamilyRules.Build"/>.</summary>
/// <remarks>
/// Lisp dialects share the bracket / quote / atom shape but vary on:
/// the brackets they accept (Clojure adds <c>[]</c> and <c>{}</c>), the
/// keyword sigil (<c>:</c> for Clojure / Scheme keywords), and the
/// declaration / constant keyword tables.
/// </remarks>
internal readonly record struct LispFamilyConfig
{
    /// <summary>Gets the declaration-keyword set (<c>defun</c>, <c>defn</c>, <c>define</c>, …).</summary>
    public ByteKeywordSet KeywordDeclarations { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for declaration keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordDeclarationFirst { get; init; }

    /// <summary>Gets the general-keyword set (<c>if</c>, <c>cond</c>, <c>let</c>, <c>lambda</c>, …).</summary>
    public ByteKeywordSet Keywords { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for general keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordFirst { get; init; }

    /// <summary>Gets the constant-keyword set (<c>nil</c>, <c>t</c>, <c>true</c>, <c>false</c>, …).</summary>
    public ByteKeywordSet KeywordConstants { get; init; }

    /// <summary>Gets the optional first-byte dispatch set for constant keywords; <see langword="null"/> falls back to <see cref="ByteKeywordSet.FirstByteSet"/>.</summary>
    public SearchValues<byte>? KeywordConstantFirst { get; init; }

    /// <summary>Gets a value indicating whether <c>[]</c> and <c>{}</c> data brackets are recognized as punctuation (Clojure / EDN).</summary>
    public bool IncludeDataBrackets { get; init; }

    /// <summary>Gets a value indicating whether <c>:keyword</c> literals are recognized (Clojure, Scheme).</summary>
    public bool IncludeColonKeyword { get; init; }
}
