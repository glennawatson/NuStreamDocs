// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Byte-level scanner for the paired-inline attr-list pattern —
/// <c>&lt;tag attrs&gt;inner&lt;/tag&gt;{: attrs }</c>. Replaces
/// <c>InlinePairedAttrListRegex</c>.
/// </summary>
internal static class InlinePairedAttrListBytes
{
    /// <summary>Length overhead of the closing tag <c>&lt;/&gt;</c> (excluding the tag name).</summary>
    private const int CloseTagOverhead = 3;

    /// <summary>Length of the <c>&lt;/</c> prefix at the start of a closing tag.</summary>
    private const int CloseTagPrefixLength = 2;

    /// <summary>ASCII bit that toggles upper-/lowercase on letters.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every paired-inline element followed by a <c>{: ... }</c> token.</summary>
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

    /// <summary>Attempts the paired-inline match starting at <paramref name="lt"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="lt">Offset of the <c>&lt;</c>.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when the element was rewritten.</returns>
    private static bool TryRewriteAt(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!AttrListTagMatcher.TryMatchInlinePairedTag(html, lt + 1, out var nameLen))
        {
            advanceTo = lt + 1;
            return false;
        }

        var nameEnd = lt + 1 + nameLen;
        var openGt = FindFirst(html, nameEnd, (byte)'>');
        if (openGt < 0)
        {
            advanceTo = lt + 1;
            return false;
        }

        var innerStart = openGt + 1;
        var closeStart = FindMatchingClose(html, innerStart, html.Slice(lt + 1, nameLen));
        if (closeStart < 0)
        {
            advanceTo = innerStart;
            return false;
        }

        var afterClose = closeStart + nameLen + CloseTagOverhead;
        if (!AttrListMarker.TryMatchMarker(html, afterClose, out var contentStart, out var contentEnd, out var markerEnd))
        {
            advanceTo = afterClose;
            return false;
        }

        var merged = AttrListMarker.ParseAndMerge(html, nameEnd, openGt, contentStart, contentEnd);
        sink.Write(html[lastEmit..nameEnd]);
        AttrListMarker.WriteString(merged, sink);
        sink.Write(html[openGt..afterClose]);
        lastEmit = markerEnd;
        advanceTo = markerEnd;
        return true;
    }

    /// <summary>Locates the closing <c>&lt;/tag&gt;</c> matching <paramref name="tagName"/>, scanning forward from <paramref name="from"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="from">Search start offset (inner-text start).</param>
    /// <param name="tagName">Tag name bytes (case-insensitive match).</param>
    /// <returns>Offset of the <c>&lt;</c> in <c>&lt;/tag&gt;</c>, or <c>-1</c> when not found.</returns>
    private static int FindMatchingClose(ReadOnlySpan<byte> html, int from, ReadOnlySpan<byte> tagName)
    {
        var p = from;
        while (p < html.Length)
        {
            var rel = html[p..].IndexOf((byte)'<');
            if (rel < 0)
            {
                return -1;
            }

            var lt = p + rel;
            if (lt + 1 < html.Length && html[lt + 1] is (byte)'/' && IsCloseFor(html, lt + CloseTagPrefixLength, tagName))
            {
                return lt;
            }

            p = lt + 1;
        }

        return -1;
    }

    /// <summary>Returns true when <paramref name="offset"/> begins a case-insensitive tag-name match terminated by <c>&gt;</c>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="offset">Offset just past <c>&lt;/</c>.</param>
    /// <param name="tagName">Tag name bytes.</param>
    /// <returns>True when this is the closing tag we're looking for.</returns>
    private static bool IsCloseFor(ReadOnlySpan<byte> html, int offset, ReadOnlySpan<byte> tagName)
    {
        if (offset + tagName.Length >= html.Length)
        {
            return false;
        }

        for (var i = 0; i < tagName.Length; i++)
        {
            if ((html[offset + i] | AsciiCaseBit) != (tagName[i] | AsciiCaseBit))
            {
                return false;
            }
        }

        return html[offset + tagName.Length] is (byte)'>';
    }

    /// <summary>Finds the first occurrence of <paramref name="b"/> at or after <paramref name="from"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="from">Search start.</param>
    /// <param name="b">Byte to find.</param>
    /// <returns>Offset of the byte, or <c>-1</c>.</returns>
    private static int FindFirst(ReadOnlySpan<byte> source, int from, byte b)
    {
        var rel = source[from..].IndexOf(b);
        return rel < 0 ? -1 : from + rel;
    }
}
