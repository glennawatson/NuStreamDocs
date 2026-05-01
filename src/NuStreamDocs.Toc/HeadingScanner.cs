// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Toc;

/// <summary>
/// Pure scanner that locates <c>&lt;h1&gt;</c> .. <c>&lt;h6&gt;</c>
/// open/close tags inside a UTF-8 HTML byte snapshot.
/// </summary>
/// <remarks>
/// The scanner is tolerant of attributes already present on the open
/// tag (it parses out an existing <c>id="..."</c> when present and
/// reports it on the resulting <see cref="Heading"/>), but it is
/// deliberately not a full HTML parser — headings nested inside
/// other elements still scan correctly because we anchor on
/// <c>&lt;hN</c> ASCII patterns.
/// </remarks>
internal static class HeadingScanner
{
    /// <summary>ASCII byte for the opening angle bracket.</summary>
    private const byte OpenAngle = (byte)'<';

    /// <summary>ASCII byte for the closing angle bracket.</summary>
    private const byte CloseAngle = (byte)'>';

    /// <summary>ASCII byte for the forward slash that begins a close tag.</summary>
    private const byte Slash = (byte)'/';

    /// <summary>Lowest supported heading level (h1).</summary>
    private const int MinHeadingLevel = 1;

    /// <summary>Highest supported heading level (h6).</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>Length of the minimum recognisable open tag stub <c>&lt;hN</c>.</summary>
    private const int OpenTagStubLength = 3;

    /// <summary>Length of a heading close tag <c>&lt;/hN&gt;</c>.</summary>
    private const int CloseTagLength = 5;

    /// <summary>Index of the <c>/</c> byte inside a close-tag candidate.</summary>
    private const int CloseTagSlashOffset = 1;

    /// <summary>Index of the <c>h</c>/<c>H</c> byte inside a close-tag candidate.</summary>
    private const int CloseTagHOffset = 2;

    /// <summary>Index of the level digit inside a close-tag candidate.</summary>
    private const int CloseTagLevelOffset = 3;

    /// <summary>Index of the closing <c>&gt;</c> inside a close-tag candidate.</summary>
    private const int CloseTagGtOffset = 4;

    /// <summary>Index of the level digit (the <c>N</c>) inside an open-tag candidate <c>&lt;hN…&gt;</c>.</summary>
    private const int LevelDigitOffset = 2;

    /// <summary>Outcome of <see cref="TryScanHeading"/>.</summary>
    private enum ScanStatus
    {
        /// <summary>A heading was successfully parsed.</summary>
        Heading,

        /// <summary>The candidate was not a heading; advance past it and keep scanning.</summary>
        NotAHeading,

        /// <summary>The buffer ended before a complete heading could be parsed.</summary>
        EndOfBuffer,
    }

    /// <summary>Scans <paramref name="html"/> for heading tags.</summary>
    /// <param name="html">Rendered HTML snapshot (UTF-8).</param>
    /// <returns>Heading records ordered by appearance.</returns>
    public static Heading[] Scan(ReadOnlySpan<byte> html)
    {
        if (html.IsEmpty)
        {
            return [];
        }

        var found = new List<Heading>(16);
        var cursor = 0;
        while (cursor < html.Length)
        {
            var openIdx = html[cursor..].IndexOf(OpenAngle);
            if (openIdx < 0)
            {
                break;
            }

            var absOpen = cursor + openIdx;
            var status = TryScanHeading(html, absOpen, out var heading, out var nextCursor);
            if (status is ScanStatus.EndOfBuffer)
            {
                break;
            }

            if (status is ScanStatus.Heading)
            {
                found.Add(heading);
            }

            cursor = nextCursor;
        }

        return [.. found];
    }

    /// <summary>Decodes the inner text content of <paramref name="heading"/> for slug + TOC label use.</summary>
    /// <param name="html">Original HTML snapshot.</param>
    /// <param name="heading">Heading record.</param>
    /// <returns>Plain-text body with HTML tags stripped.</returns>
    public static string DecodeText(ReadOnlySpan<byte> html, in Heading heading)
    {
        var inner = html[heading.TextStart..heading.TextEnd];
        if (inner.IsEmpty)
        {
            return string.Empty;
        }

        // Strip nested tags. Most headings are tag-free; when they do contain
        // markup (e.g. <code>, <em>) we just want the text. We accumulate
        // text-only byte spans then UTF-8 decode the joined span so non-ASCII
        // characters survive intact.
        var sb = new StringBuilder(inner.Length);
        var i = 0;
        var textStart = 0;
        while (i < inner.Length)
        {
            if (inner[i] is OpenAngle)
            {
                if (i > textStart)
                {
                    sb.Append(Encoding.UTF8.GetString(inner[textStart..i]));
                }

                var closeRel = inner[i..].IndexOf(CloseAngle);
                if (closeRel < 0)
                {
                    textStart = inner.Length;
                    break;
                }

                i += closeRel + 1;
                textStart = i;
                continue;
            }

            i++;
        }

        if (textStart < inner.Length)
        {
            sb.Append(Encoding.UTF8.GetString(inner[textStart..]));
        }

        return sb.ToString().Trim();
    }

    /// <summary>Finds the next <c>&lt;/hN&gt;</c> close tag.</summary>
    /// <param name="html">Full snapshot.</param>
    /// <param name="from">Search-start offset.</param>
    /// <param name="level">Expected heading level.</param>
    /// <returns>Offset of the close-tag <c>&lt;</c>, or -1 if none.</returns>
    private static int FindCloseTag(ReadOnlySpan<byte> html, int from, int level)
    {
        var cursor = from;
        var levelByte = (byte)('0' + level);
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOf(OpenAngle);
            if (rel < 0)
            {
                return -1;
            }

            var pos = cursor + rel;
            if (pos + CloseTagGtOffset >= html.Length)
            {
                return -1;
            }

            if (html[pos + CloseTagSlashOffset] is Slash
                && html[pos + CloseTagHOffset] is (byte)'h' or (byte)'H'
                && html[pos + CloseTagLevelOffset] == levelByte
                && html[pos + CloseTagGtOffset] is CloseAngle)
            {
                return pos;
            }

            cursor = pos + 1;
        }

        return -1;
    }

    /// <summary>Extracts the value of an <c>id="..."</c> attribute from the open-tag span.</summary>
    /// <param name="openTag">The full <c>&lt;hN ...&gt;</c> bytes.</param>
    /// <returns>The id value, or empty when absent.</returns>
    private static string ExtractIdAttribute(ReadOnlySpan<byte> openTag)
    {
        var needle = "id="u8;
        for (var i = 0; i < openTag.Length - needle.Length; i++)
        {
            if (openTag[i] is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                continue;
            }

            var slice = openTag[(i + 1)..];
            if (slice.StartsWith(needle))
            {
                return ReadAttributeValue(slice[needle.Length..]);
            }
        }

        return string.Empty;
    }

    /// <summary>Reads a quoted (or unquoted) attribute value from the start of <paramref name="rest"/>.</summary>
    /// <param name="rest">Bytes immediately after <c>id=</c>.</param>
    /// <returns>The decoded value.</returns>
    private static string ReadAttributeValue(ReadOnlySpan<byte> rest)
    {
        if (rest.IsEmpty)
        {
            return string.Empty;
        }

        var quote = rest[0];
        return quote is (byte)'"' or (byte)'\''
            ? ReadQuotedAttributeValue(rest, quote)
            : ReadUnquotedAttributeValue(rest);
    }

    /// <summary>Reads a quoted attribute value. Returns empty when the closing quote is missing.</summary>
    /// <param name="rest">Bytes starting at the opening quote.</param>
    /// <param name="quote">The quote byte (<c>"</c> or <c>'</c>).</param>
    /// <returns>The decoded value.</returns>
    private static string ReadQuotedAttributeValue(ReadOnlySpan<byte> rest, byte quote)
    {
        var end = rest[1..].IndexOf(quote);
        return end < 0 ? string.Empty : Encoding.UTF8.GetString(rest.Slice(1, end));
    }

    /// <summary>Reads an unquoted attribute value, stopping at whitespace or <c>&gt;</c>.</summary>
    /// <param name="rest">Bytes starting at the value's first char.</param>
    /// <returns>The decoded value.</returns>
    private static string ReadUnquotedAttributeValue(ReadOnlySpan<byte> rest)
    {
        var stop = 0;
        while (stop < rest.Length)
        {
            var b = rest[stop];
            if (b is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or CloseAngle)
            {
                break;
            }

            stop++;
        }

        return Encoding.UTF8.GetString(rest[..stop]);
    }

    /// <summary>Attempts to parse a heading whose <c>&lt;</c> sits at <paramref name="absOpen"/>.</summary>
    /// <param name="html">Full HTML snapshot.</param>
    /// <param name="absOpen">Absolute offset of the candidate <c>&lt;</c>.</param>
    /// <param name="heading">Populated heading on success; default otherwise.</param>
    /// <param name="nextCursor">Advance offset for the outer loop.</param>
    /// <returns>The classification of the candidate.</returns>
    private static ScanStatus TryScanHeading(
        ReadOnlySpan<byte> html,
        int absOpen,
        out Heading heading,
        out int nextCursor)
    {
        heading = default;
        nextCursor = absOpen + 1;

        // Need at least "<hN>" before the buffer ends.
        if (absOpen + OpenTagStubLength >= html.Length)
        {
            nextCursor = html.Length;
            return ScanStatus.EndOfBuffer;
        }

        if (!IsHeadingOpenStub(html, absOpen, out var level))
        {
            return ScanStatus.NotAHeading;
        }

        var closeAngleRel = html[(absOpen + OpenTagStubLength)..].IndexOf(CloseAngle);
        if (closeAngleRel < 0)
        {
            nextCursor = html.Length;
            return ScanStatus.EndOfBuffer;
        }

        var openTagEnd = absOpen + OpenTagStubLength + closeAngleRel + 1;
        var existingId = ExtractIdAttribute(html[absOpen..openTagEnd]);

        var closeIdx = FindCloseTag(html, openTagEnd, level);
        if (closeIdx < 0)
        {
            nextCursor = openTagEnd;
            return ScanStatus.NotAHeading;
        }

        heading = new(
            Level: level,
            OpenTagStart: absOpen,
            OpenTagEnd: openTagEnd,
            CloseTagStart: closeIdx,
            TextStart: openTagEnd,
            TextEnd: closeIdx,
            ExistingId: existingId,
            Slug: string.Empty);
        nextCursor = closeIdx + CloseTagLength;
        return ScanStatus.Heading;
    }

    /// <summary>Tests whether the bytes at <paramref name="absOpen"/> form the start of a heading open tag.</summary>
    /// <param name="html">Full HTML snapshot.</param>
    /// <param name="absOpen">Offset of the candidate <c>&lt;</c>.</param>
    /// <param name="level">Decoded heading level when the test passes.</param>
    /// <returns>True if the bytes are <c>&lt;hN</c> with N in 1..6 and a valid trailing byte.</returns>
    private static bool IsHeadingOpenStub(ReadOnlySpan<byte> html, int absOpen, out int level)
    {
        level = 0;
        var maybeH = html[absOpen + 1];
        if (maybeH is not ((byte)'h' or (byte)'H'))
        {
            return false;
        }

        level = html[absOpen + LevelDigitOffset] - (byte)'0';
        if (level is < MinHeadingLevel or > MaxHeadingLevel)
        {
            return false;
        }

        var afterLevel = html[absOpen + OpenTagStubLength];
        return afterLevel is (byte)'>' or (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';
    }
}
