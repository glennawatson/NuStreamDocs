// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.MarkdownExtensions.AttrList.Bytes;

/// <summary>
/// Shared low-level HTML tag scanning helpers for attr-list byte rewriters.
/// </summary>
internal static class AttrListTagScanner
{
    /// <summary>Length overhead of the closing tag <c>&lt;/&gt;</c> (excluding the tag name).</summary>
    public const int CloseTagOverhead = 3;

    /// <summary>Length of the <c>&lt;/</c> prefix at the start of a closing tag.</summary>
    private const int CloseTagPrefixLength = 2;

    /// <summary>ASCII bit that toggles upper-/lowercase on letters.</summary>
    private const byte AsciiCaseBit = 0x20;

    /// <summary>Finds the first occurrence of <paramref name="b"/> at or after <paramref name="from"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="from">Search start.</param>
    /// <param name="b">Byte to find.</param>
    /// <returns>Offset of the byte, or <c>-1</c>.</returns>
    public static int FindFirst(ReadOnlySpan<byte> source, int from, byte b)
    {
        var rel = source[from..].IndexOf(b);
        return rel < 0 ? -1 : from + rel;
    }

    /// <summary>Locates the closing <c>&lt;/tag&gt;</c> matching <paramref name="tagName"/>, scanning forward from <paramref name="from"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="from">Search start offset (inner-text start).</param>
    /// <param name="tagName">Tag name bytes (case-insensitive match).</param>
    /// <returns>Offset of the <c>&lt;</c> in <c>&lt;/tag&gt;</c>, or <c>-1</c> when not found.</returns>
    public static int FindMatchingClose(ReadOnlySpan<byte> html, int from, ReadOnlySpan<byte> tagName)
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
    /// <returns>True when this is the closing tag being sought.</returns>
    public static bool IsCloseFor(ReadOnlySpan<byte> html, int offset, ReadOnlySpan<byte> tagName)
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
}
