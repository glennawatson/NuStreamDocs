// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// Vectorized marker probes used by <c>IMarkdownPreprocessor.NeedsRewrite</c>
/// implementations to short-circuit per-page rewrites when the source
/// contains none of the plugin's distinctive bytes.
/// </summary>
/// <remarks>
/// The probes intentionally accept some false-positives (e.g.
/// <see cref="HasMarkSpan"/> matches any <c>==</c> sequence, which can
/// also appear in code blocks) — the plugin's full rewriter will still
/// scan precisely. The goal here is the cheap "definitely no markers"
/// answer that lets the build pipeline skip the rewriter entirely.
/// </remarks>
public static class MarkdownMarkerProbes
{
    /// <summary>Single-byte search set for caret / tilde markers (<c>^</c> + <c>~</c>).</summary>
    private static readonly SearchValues<byte> CaretOrTildeBytes = SearchValues.Create("^~"u8);

    /// <summary>True when <paramref name="source"/> contains an admonition opener (<c>!!! </c>).</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasAdmonitionOpener(ReadOnlySpan<byte> source) =>
        source.IndexOf("!!! "u8) >= 0;

    /// <summary>True when <paramref name="source"/> contains a details opener (<c>??? </c> or <c>???+</c>) — both share the <c>???</c> prefix.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasDetailsOpener(ReadOnlySpan<byte> source) =>
        source.IndexOf("???"u8) >= 0;

    /// <summary>True when <paramref name="source"/> contains a content-tabs opener (<c>=== "</c>).</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasTabsOpener(ReadOnlySpan<byte> source) =>
        source.IndexOf("=== \""u8) >= 0;

    /// <summary>True when <paramref name="source"/> contains a check-list bullet (<c>- [</c>).</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasCheckListBullet(ReadOnlySpan<byte> source) =>
        source.IndexOf("- ["u8) >= 0;

    /// <summary>True when <paramref name="source"/> contains a mark span (<c>==</c>).</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasMarkSpan(ReadOnlySpan<byte> source) =>
        source.IndexOf("=="u8) >= 0;

    /// <summary>True when <paramref name="source"/> contains a footnote reference or definition (<c>[^</c>).</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasFootnoteMarker(ReadOnlySpan<byte> source) =>
        source.IndexOf("[^"u8) >= 0;

    /// <summary>True when <paramref name="source"/> contains a pymdownx-style inline-hilite fence (<c>`#!</c>).</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasInlineHiliteFence(ReadOnlySpan<byte> source) =>
        source.IndexOf("`#!"u8) >= 0;

    /// <summary>True when <paramref name="source"/> contains the <c>markdown=</c> attribute used by md-in-html.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasMdInHtmlAttribute(ReadOnlySpan<byte> source) =>
        source.IndexOf("markdown="u8) >= 0;

    /// <summary>True when <paramref name="source"/> contains either a caret (<c>^</c>) or tilde (<c>~</c>) byte — used by sub/sup/ins/del rewriters.</summary>
    /// <param name="source">UTF-8 markdown source.</param>
    /// <returns>True when the source contains the marker.</returns>
    public static bool HasCaretOrTilde(ReadOnlySpan<byte> source) =>
        source.IndexOfAny(CaretOrTildeBytes) >= 0;
}
