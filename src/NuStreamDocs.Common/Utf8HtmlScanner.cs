// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Stateless byte-level helpers for the rendered-HTML pipelines —
/// finding heading open tags, locating attribute values inside an
/// open-tag span — without ever decoding the snapshot to UTF-16.
/// </summary>
/// <remarks>
/// The renderer's emitter is the only authoritative source of these
/// tags so the shape is stable; this is a tolerant scanner, not a
/// full HTML parser. Three plugins share it: <c>Toc</c> (id splice +
/// permalink anchor), <c>Autorefs</c> (heading-id registry build), and
/// <c>LinkValidator</c> (per-page link/id inventory).
/// </remarks>
public static class Utf8HtmlScanner
{
    /// <summary>ASCII byte for the opening angle bracket.</summary>
    private const byte OpenAngle = (byte)'<';

    /// <summary>ASCII byte for the closing angle bracket.</summary>
    private const byte CloseAngle = (byte)'>';

    /// <summary>Lowest supported heading level.</summary>
    private const int MinHeadingLevel = 1;

    /// <summary>Highest supported heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>Bytes consumed by the <c>&lt;hN</c> stub before the level digit's trailing byte.</summary>
    private const int OpenTagStubLength = 3;

    /// <summary>Locates the next <c>&lt;h1&gt;</c>..<c>&lt;h6&gt;</c> open tag at or after <paramref name="from"/>.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <param name="from">Search-start offset.</param>
    /// <param name="tagStart">Index of the leading <c>&lt;</c> on success.</param>
    /// <param name="tagEnd">Index just past the closing <c>&gt;</c> on success.</param>
    /// <param name="level">Decoded heading level (1..6) on success.</param>
    /// <returns>True when a heading open tag was found.</returns>
    /// <remarks>
    /// The trailing byte after the level digit must be <c>&gt;</c> or
    /// ASCII whitespace, so <c>&lt;h1foo&gt;</c> is correctly rejected.
    /// Case-insensitive on the <c>h</c>.
    /// </remarks>
    public static bool TryFindNextHeadingOpen(
        ReadOnlySpan<byte> html,
        int from,
        out int tagStart,
        out int tagEnd,
        out int level)
    {
        tagStart = -1;
        tagEnd = -1;
        level = 0;

        var cursor = from;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf(OpenAngle);
            if (rel < 0)
            {
                return false;
            }

            var i = cursor + rel;
            if (i + OpenTagStubLength >= html.Length)
            {
                return false;
            }

            if (!IsHeadingStub(html, i, out var detectedLevel))
            {
                cursor = i + 1;
                continue;
            }

            var closeRel = html[(i + OpenTagStubLength)..].IndexOf(CloseAngle);
            if (closeRel < 0)
            {
                return false;
            }

            tagStart = i;
            tagEnd = i + OpenTagStubLength + closeRel + 1;
            level = detectedLevel;
            return true;
        }

        return false;
    }

    /// <summary>Locates the value of <paramref name="attrName"/> inside <paramref name="openTag"/>.</summary>
    /// <param name="openTag">Bytes from the leading <c>&lt;</c> up to and including the closing <c>&gt;</c> (or any prefix that contains the attribute).</param>
    /// <param name="attrName">Attribute name in lowercase ASCII (e.g. <c>"id"u8</c>, <c>"href"u8</c>).</param>
    /// <returns>The local offset+length of the value into <paramref name="openTag"/>, or <c>(-1, 0)</c> when the attribute is missing or malformed.</returns>
    /// <remarks>
    /// Handles quoted (<c>"</c> / <c>'</c>) and unquoted attribute
    /// values, and tolerates any ASCII whitespace before the attribute
    /// name. Match is exact ASCII; pass the lowercase form.
    /// </remarks>
    public static (int Start, int Length) FindAttributeValue(
        ReadOnlySpan<byte> openTag,
        ReadOnlySpan<byte> attrName)
    {
        if (attrName.IsEmpty)
        {
            return (-1, 0);
        }

        // Walk every whitespace boundary. The needle is "name=" so we
        // can rule out partial-name matches like "data-id=" trying to
        // match "id=".
        var nameLen = attrName.Length;
        for (var i = 0; i < openTag.Length - nameLen - 1; i++)
        {
            if (!AsciiByteHelpers.IsAsciiWhitespace(openTag[i]))
            {
                continue;
            }

            var afterWs = i + 1;
            if (afterWs + nameLen + 1 > openTag.Length)
            {
                return (-1, 0);
            }

            if (!openTag.Slice(afterWs, nameLen).SequenceEqual(attrName))
            {
                continue;
            }

            if (openTag[afterWs + nameLen] is not (byte)'=')
            {
                continue;
            }

            return ReadAttributeValue(openTag, afterWs + nameLen + 1);
        }

        return (-1, 0);
    }

    /// <summary>True when <paramref name="html"/> starts a heading open-tag at <paramref name="absOpen"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="absOpen">Index of the candidate <c>&lt;</c>.</param>
    /// <param name="level">Decoded heading level when the test passes.</param>
    /// <returns>True for <c>&lt;hN</c> with N in 1..6 followed by <c>&gt;</c> / whitespace.</returns>
    private static bool IsHeadingStub(ReadOnlySpan<byte> html, int absOpen, out int level)
    {
        level = 0;
        var maybeH = html[absOpen + 1];
        if (maybeH is not ((byte)'h' or (byte)'H'))
        {
            return false;
        }

        var detectedLevel = html[absOpen + 2] - (byte)'0';
        if (detectedLevel is < MinHeadingLevel or > MaxHeadingLevel)
        {
            return false;
        }

        var afterLevel = html[absOpen + OpenTagStubLength];
        if (afterLevel is not (byte)'>' && !AsciiByteHelpers.IsAsciiWhitespace(afterLevel))
        {
            return false;
        }

        level = detectedLevel;
        return true;
    }

    /// <summary>Reads a quoted or unquoted attribute value at <paramref name="valueStart"/> in <paramref name="openTag"/>.</summary>
    /// <param name="openTag">Open-tag bytes.</param>
    /// <param name="valueStart">Index of the value's first byte (the opening quote, if any).</param>
    /// <returns>Local offset+length, or <c>(-1, 0)</c> when malformed.</returns>
    private static (int Start, int Length) ReadAttributeValue(ReadOnlySpan<byte> openTag, int valueStart)
    {
        if (valueStart >= openTag.Length)
        {
            return (-1, 0);
        }

        var quote = openTag[valueStart];
        if (quote is (byte)'"' or (byte)'\'')
        {
            var end = openTag[(valueStart + 1)..].IndexOf(quote);
            return end < 0 ? (-1, 0) : (valueStart + 1, end);
        }

        var stop = ScanUnquotedValueLength(openTag[valueStart..]);
        return stop is 0 ? (-1, 0) : (valueStart, stop);
    }

    /// <summary>Returns the length of an unquoted attribute value at the start of <paramref name="rest"/>, stopping at whitespace or <c>&gt;</c>.</summary>
    /// <param name="rest">Bytes starting at the value's first char.</param>
    /// <returns>Byte count consumed.</returns>
    private static int ScanUnquotedValueLength(ReadOnlySpan<byte> rest)
    {
        for (var i = 0; i < rest.Length; i++)
        {
            var b = rest[i];
            if (b is CloseAngle || AsciiByteHelpers.IsAsciiWhitespace(b))
            {
                return i;
            }
        }

        return rest.Length;
    }
}
