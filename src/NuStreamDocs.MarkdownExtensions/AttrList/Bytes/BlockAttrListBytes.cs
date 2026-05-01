// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Byte-level scanner for the block-level attr-list pattern —
/// <c>&lt;tag&gt;prefix {: attrs } suffix&lt;/tag&gt;</c>. Replaces
/// <c>BlockAttrListRegex</c>.
/// </summary>
internal static class BlockAttrListBytes
{
    /// <summary>Length overhead of the closing tag <c>&lt;/&gt;</c> (excluding the tag name).</summary>
    private const int CloseTagOverhead = 3;

    /// <summary>Length of the <c>&lt;/</c> prefix at the start of a closing tag.</summary>
    private const int CloseTagPrefixLength = 2;

    /// <summary>ASCII bit that toggles upper-/lowercase on letters.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every block element whose text content ends with a <c>{: ... }</c> token.</summary>
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

    /// <summary>Attempts the block match starting at <paramref name="lt"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="lt">Offset of <c>&lt;</c>.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when the element was rewritten.</returns>
    private static bool TryRewriteAt(ReadOnlySpan<byte> html, int lt, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!AttrListTagMatcher.TryMatchBlockTag(html, lt + 1, out var nameLen))
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
        var tagName = html.Slice(lt + 1, nameLen);
        var closeStart = FindClose(html, innerStart, tagName);
        if (closeStart < 0)
        {
            advanceTo = innerStart;
            return false;
        }

        if (!TryFindMarkerInside(html, innerStart, closeStart, out var prefixEnd, out var contentStart, out var contentEnd, out var suffixStart))
        {
            advanceTo = closeStart + nameLen + CloseTagOverhead;
            return false;
        }

        var match = new BlockMatch(nameEnd, openGt, prefixEnd, contentStart, contentEnd, suffixStart, closeStart, nameLen);
        EmitRewritten(html, sink, ref lastEmit, match);
        advanceTo = closeStart + nameLen + CloseTagOverhead;
        return true;
    }

    /// <summary>Emits the rewritten block element into the sink.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to (updated).</param>
    /// <param name="m">Match positions.</param>
    private static void EmitRewritten(ReadOnlySpan<byte> html, IBufferWriter<byte> sink, ref int lastEmit, in BlockMatch m)
    {
        var merged = AttrListMarker.ParseAndMerge(html, m.NameEnd, m.OpenGt, m.ContentStart, m.ContentEnd);
        sink.Write(html[lastEmit..m.NameEnd]);
        AttrListMarker.WriteString(merged, sink);
        var openGtEnd = m.OpenGt + 1;
        sink.Write(html[m.OpenGt..openGtEnd]);
        sink.Write(html[openGtEnd..m.PrefixEnd]);
        sink.Write(html[m.SuffixStart..m.CloseStart]);
        var closeEnd = m.CloseStart + m.NameLen + CloseTagOverhead;
        sink.Write(html[m.CloseStart..closeEnd]);
        lastEmit = closeEnd;
    }

    /// <summary>Locates a <c>{: ... }</c> token between <paramref name="innerStart"/> and <paramref name="closeStart"/>, splitting the body into prefix / content / suffix.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="innerStart">Inner-content start offset.</param>
    /// <param name="closeStart">Offset of <c>&lt;</c> in the closing tag.</param>
    /// <param name="prefixEnd">Set to the offset just past the prefix text (trimmed of trailing whitespace).</param>
    /// <param name="contentStart">Set to the offset of the inner attr-list text.</param>
    /// <param name="contentEnd">Set to the offset just past the inner attr-list text.</param>
    /// <param name="suffixStart">Set to the offset where the suffix text begins.</param>
    /// <returns>True when a marker was found.</returns>
    private static bool TryFindMarkerInside(ReadOnlySpan<byte> html, int innerStart, int closeStart, out int prefixEnd, out int contentStart, out int contentEnd, out int suffixStart)
    {
        prefixEnd = -1;
        contentStart = -1;
        contentEnd = -1;
        suffixStart = -1;

        var inner = html[innerStart..closeStart];
        var rel = inner.IndexOf(AttrListMarker.OpenMarker);
        if (rel < 0)
        {
            return false;
        }

        var openMarker = innerStart + rel;
        var trimmedPrefixEnd = openMarker;
        while (trimmedPrefixEnd > innerStart && html[trimmedPrefixEnd - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            trimmedPrefixEnd--;
        }

        if (!AttrListMarker.TryMatchMarker(html, openMarker, out contentStart, out contentEnd, out var markerEnd))
        {
            return false;
        }

        prefixEnd = trimmedPrefixEnd;

        // Suffix is `[^<]*` per regex — scans from markerEnd up to closeStart with no '<' allowed.
        var suffix = html[markerEnd..closeStart];
        if (suffix.IndexOf((byte)'<') >= 0)
        {
            return false;
        }

        suffixStart = markerEnd;
        return true;
    }

    /// <summary>Finds the closing <c>&lt;/tag&gt;</c> matching <paramref name="tagName"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="from">Search start (inner-text start).</param>
    /// <param name="tagName">Tag-name bytes.</param>
    /// <returns>Offset of <c>&lt;</c> in <c>&lt;/tag&gt;</c>, or <c>-1</c>.</returns>
    private static int FindClose(ReadOnlySpan<byte> html, int from, ReadOnlySpan<byte> tagName)
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
    /// <returns>True on a match.</returns>
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
