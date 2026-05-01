// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Extracts <c>href</c> / <c>src</c> URL values and heading anchor IDs
/// from rendered HTML.
/// </summary>
/// <remarks>
/// Byte-level walk; no HTML parser. The renderer's emitter is the only
/// authoritative source of these tags so the shape is stable. Returns
/// arrays so callers can iterate without allocating an enumerator.
/// </remarks>
public static class LinkExtractor
{
    /// <summary>Bytes after <c>&lt;</c> that the heading scanner reads: one for <c>h</c>, one for the level digit.</summary>
    private const int HeadingTagLookahead = 2;

    /// <summary>Extracts every <c>href</c> attribute value from <paramref name="html"/>.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Decoded link values in document order.</returns>
    public static string[] ExtractHrefs(ReadOnlySpan<byte> html) =>
        ExtractAttribute(html, " href=\""u8);

    /// <summary>Extracts every <c>src</c> attribute value from <paramref name="html"/>.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Decoded src values in document order.</returns>
    public static string[] ExtractSources(ReadOnlySpan<byte> html) =>
        ExtractAttribute(html, " src=\""u8);

    /// <summary>Extracts every heading <c>id</c> attribute value from <paramref name="html"/>.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Decoded id values in document order.</returns>
    public static string[] ExtractHeadingIds(ReadOnlySpan<byte> html)
    {
        var ids = new List<string>(8);
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rest = html[cursor..];
            var lt = rest.IndexOf((byte)'<');
            if (lt < 0)
            {
                break;
            }

            var tagStart = cursor + lt;
            if (!IsHeadingOpen(html, tagStart))
            {
                cursor = tagStart + 1;
                continue;
            }

            var tagEnd = html[tagStart..].IndexOf((byte)'>');
            if (tagEnd < 0)
            {
                break;
            }

            var openTag = html.Slice(tagStart, tagEnd);
            if (TryExtractAttribute(openTag, " id=\""u8, out var id))
            {
                ids.Add(id);
            }

            cursor = tagStart + tagEnd + 1;
        }

        return [.. ids];
    }

    /// <summary>Walks <paramref name="html"/> and returns every <paramref name="marker"/> attribute value.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <param name="marker">Attribute marker (e.g. <c> href="</c>).</param>
    /// <returns>Decoded values in document order.</returns>
    private static string[] ExtractAttribute(ReadOnlySpan<byte> html, ReadOnlySpan<byte> marker)
    {
        var values = new List<string>(16);
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rest = html[cursor..];
            var hit = rest.IndexOf(marker);
            if (hit < 0)
            {
                break;
            }

            var valueStart = cursor + hit + marker.Length;
            var valueEnd = html[valueStart..].IndexOf((byte)'"');
            if (valueEnd < 0)
            {
                break;
            }

            values.Add(Encoding.UTF8.GetString(html.Slice(valueStart, valueEnd)));
            cursor = valueStart + valueEnd + 1;
        }

        return [.. values];
    }

    /// <summary>True when the bytes at <paramref name="index"/> open a heading tag (<c>&lt;h1</c>..<c>&lt;h6</c>).</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <param name="index">Index of the leading <c>&lt;</c>.</param>
    /// <returns>True when the next two bytes are <c>h</c> + a level digit.</returns>
    private static bool IsHeadingOpen(ReadOnlySpan<byte> html, int index)
    {
        if (index + HeadingTagLookahead >= html.Length)
        {
            return false;
        }

        if (html[index + 1] is not ((byte)'h' or (byte)'H'))
        {
            return false;
        }

        return html[index + HeadingTagLookahead] is >= (byte)'1' and <= (byte)'6';
    }

    /// <summary>Tries to read one named attribute from a tag's bytes.</summary>
    /// <param name="openTag">Tag bytes between the leading <c>&lt;</c> and the closing <c>&gt;</c>.</param>
    /// <param name="marker">Attribute marker (e.g. <c> id="</c>).</param>
    /// <param name="value">Decoded attribute value on success.</param>
    /// <returns>True when the attribute was found.</returns>
    private static bool TryExtractAttribute(ReadOnlySpan<byte> openTag, ReadOnlySpan<byte> marker, out string value)
    {
        var pos = openTag.IndexOf(marker);
        if (pos < 0)
        {
            value = string.Empty;
            return false;
        }

        var valueStart = pos + marker.Length;
        var valueEnd = openTag[valueStart..].IndexOf((byte)'"');
        if (valueEnd <= 0)
        {
            value = string.Empty;
            return false;
        }

        value = Encoding.UTF8.GetString(openTag.Slice(valueStart, valueEnd));
        return true;
    }
}
