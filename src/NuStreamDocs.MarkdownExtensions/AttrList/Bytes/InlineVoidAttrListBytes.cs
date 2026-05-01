// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Byte-level scanner for the void-inline attr-list pattern —
/// <c>&lt;img attrs[/]&gt;{: attrs }</c>. Replaces
/// <c>InlineVoidAttrListRegex</c>.
/// </summary>
internal static class InlineVoidAttrListBytes
{
    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every void inline element followed by a <c>{: ... }</c> token.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one element was rewritten.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var changed = false;
        var lastEmit = 0;
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf((byte)'<');
            if (rel < 0)
            {
                break;
            }

            var lt = cursor + rel;
            if (TryRewriteAt(html, lt, sink, ref lastEmit, out var advanceTo))
            {
                changed = true;
            }

            cursor = advanceTo;
        }

        if (!changed)
        {
            return false;
        }

        sink.Write(html[lastEmit..]);
        return true;
    }

    /// <summary>Attempts the void-inline match at <paramref name="lt"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="lt">Offset of the <c>&lt;</c>.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when the element was rewritten.</returns>
    private static bool TryRewriteAt(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!AttrListTagMatcher.TryMatchInlineVoidTag(html, lt + 1, out var nameLen))
        {
            advanceTo = lt + 1;
            return false;
        }

        var nameEnd = lt + 1 + nameLen;
        var gt = FindTagEnd(html, nameEnd);
        if (gt < 0)
        {
            advanceTo = lt + 1;
            return false;
        }

        var slashStart = FindSlashRunStart(html, nameEnd, gt);
        if (!AttrListMarker.TryMatchMarker(html, gt + 1, out var contentStart, out var contentEnd, out var markerEnd))
        {
            advanceTo = gt + 1;
            return false;
        }

        var merged = AttrListMarker.ParseAndMerge(html, nameEnd, slashStart, contentStart, contentEnd);
        sink.Write(html[lastEmit..nameEnd]);
        AttrListMarker.WriteString(merged, sink);
        sink.Write(html[slashStart..(gt + 1)]);
        lastEmit = markerEnd;
        advanceTo = markerEnd;
        return true;
    }

    /// <summary>Finds the closing <c>&gt;</c> from a starting offset; returns <c>-1</c> when missing.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="from">Search start.</param>
    /// <returns>Offset of the <c>&gt;</c>, or <c>-1</c>.</returns>
    private static int FindTagEnd(ReadOnlySpan<byte> html, int from)
    {
        var rel = html[from..].IndexOf((byte)'>');
        return rel < 0 ? -1 : from + rel;
    }

    /// <summary>Returns the offset of the start of the <c>\s*/</c> run before <c>&gt;</c>, or <paramref name="gt"/> when there's no <c>/</c> directly before <c>&gt;</c>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="nameEnd">Offset just past the tag name.</param>
    /// <param name="gt">Offset of the closing <c>&gt;</c>.</param>
    /// <returns>Offset where the <c>\s*/</c> run begins, or <paramref name="gt"/> when no slash is present.</returns>
    private static int FindSlashRunStart(ReadOnlySpan<byte> html, int nameEnd, int gt)
    {
        if (gt - 1 < nameEnd || html[gt - 1] is not (byte)'/')
        {
            return gt;
        }

        var ws = gt - 1;
        while (ws > nameEnd && html[ws - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            ws--;
        }

        return ws;
    }
}
