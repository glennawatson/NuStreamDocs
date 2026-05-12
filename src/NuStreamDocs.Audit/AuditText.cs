// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>Text-content helpers shared by the accessibility lints.</summary>
internal static class AuditText
{
    /// <summary>Length of an end-tag prefix <c>&lt;/</c>.</summary>
    private const int EndTagPrefixLength = 2;

    /// <summary>Tests whether an attribute run carries a non-empty <c>aria-label</c>, <c>aria-labelledby</c>, or <c>title</c>.</summary>
    /// <param name="attributes">Attribute text from a tag.</param>
    /// <returns><see langword="true"/> when one of those attributes is present with a non-whitespace value.</returns>
    public static bool HasAccessibleNameAttribute(ReadOnlySpan<byte> attributes) =>
        HasNonEmptyAttribute(attributes, "aria-label"u8)
        || HasNonEmptyAttribute(attributes, "aria-labelledby"u8)
        || HasNonEmptyAttribute(attributes, "title"u8);

    /// <summary>Tests whether <paramref name="name"/> is present on the attribute run with a non-whitespace value.</summary>
    /// <param name="attributes">Attribute text from a tag.</param>
    /// <param name="name">Attribute name.</param>
    /// <returns><see langword="true"/> when present and non-empty.</returns>
    public static bool HasNonEmptyAttribute(ReadOnlySpan<byte> attributes, ReadOnlySpan<byte> name) =>
        HtmlAttr.TryGet(attributes, name, out var value) && HasText(value);

    /// <summary>True when <paramref name="bytes"/> contains at least one byte that is not ASCII whitespace.</summary>
    /// <param name="bytes">Bytes to scan.</param>
    /// <returns><see langword="true"/> when non-whitespace content is present.</returns>
    public static bool HasText(ReadOnlySpan<byte> bytes) =>
        !AsciiByteHelpers.IsAllAsciiWhitespace(bytes);

    /// <summary>
    /// Tests whether the inner HTML of an interactive element provides discernible content — visible
    /// text outside of tags, or a descendant that plausibly carries an accessible name (an image,
    /// an inline SVG, a <c>&lt;picture&gt;</c>, or an <c>aria-label</c>). Deliberately conservative
    /// so legitimately-labeled controls are never flagged.
    /// </summary>
    /// <param name="innerHtml">The bytes between the element's start and end tags.</param>
    /// <returns><see langword="true"/> when the element is not empty.</returns>
    public static bool HasDiscernibleContent(ReadOnlySpan<byte> innerHtml)
    {
        if (ContainsLikelyNamedDescendant(innerHtml))
        {
            return true;
        }

        HtmlTagCursor cursor = new(innerHtml);
        var textStart = 0;
        while (cursor.MoveNext())
        {
            if (HasText(innerHtml[textStart..cursor.TagStart]))
            {
                return true;
            }

            textStart = cursor.TagEnd;
        }

        return HasText(innerHtml[textStart..]);
    }

    /// <summary>Finds the offset of the <c>&lt;</c> that opens the next <c>&lt;/name&gt;</c> end tag at or after <paramref name="searchFrom"/>.</summary>
    /// <param name="html">UTF-8 HTML bytes.</param>
    /// <param name="searchFrom">Offset to start searching from.</param>
    /// <param name="name">Tag name whose end tag to find.</param>
    /// <returns>The offset of the end tag's <c>&lt;</c>, or <c>-1</c> when no matching end tag exists.</returns>
    public static int FindCloseTag(ReadOnlySpan<byte> html, int searchFrom, ReadOnlySpan<byte> name)
    {
        var i = searchFrom;
        while (i < html.Length)
        {
            var rel = html[i..].IndexOf((byte)'<');
            if (rel < 0)
            {
                return -1;
            }

            var lt = i + rel;
            if (IsEndTagAt(html, lt, name))
            {
                return lt;
            }

            i = lt + 1;
        }

        return -1;
    }

    /// <summary>True when an <c>&lt;/name&gt;</c> end tag begins at <paramref name="lt"/>.</summary>
    /// <param name="html">UTF-8 HTML bytes.</param>
    /// <param name="lt">Offset of a candidate <c>&lt;</c>.</param>
    /// <param name="name">Tag name to match.</param>
    /// <returns><see langword="true"/> on a match.</returns>
    private static bool IsEndTagAt(ReadOnlySpan<byte> html, int lt, ReadOnlySpan<byte> name)
    {
        var nameStart = lt + EndTagPrefixLength;
        var after = nameStart + name.Length;
        if (lt + 1 >= html.Length || html[lt + 1] != (byte)'/' || after > html.Length)
        {
            return false;
        }

        if (!AsciiByteHelpers.EqualsIgnoreAsciiCase(html.Slice(nameStart, name.Length), name))
        {
            return false;
        }

        return after == html.Length || html[after] is (byte)'>' or (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
    }

    /// <summary>True when <paramref name="innerHtml"/> contains a descendant that plausibly carries an accessible name.</summary>
    /// <param name="innerHtml">Inner HTML bytes of an interactive element.</param>
    /// <returns><see langword="true"/> when an image, inline SVG, picture, or <c>aria-label</c> appears.</returns>
    private static bool ContainsLikelyNamedDescendant(ReadOnlySpan<byte> innerHtml) =>
        innerHtml.IndexOf("aria-label"u8) >= 0
        || innerHtml.IndexOf("<svg"u8) >= 0
        || innerHtml.IndexOf("<img"u8) >= 0
        || innerHtml.IndexOf("<picture"u8) >= 0;
}
