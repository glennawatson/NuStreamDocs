// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Toc;

/// <summary>
/// Pure scanner that locates <c>&lt;h1&gt;</c> .. <c>&lt;h6&gt;</c>
/// open/close tag pairs inside a UTF-8 HTML byte snapshot.
/// </summary>
/// <remarks>
/// Built on top of <see cref="Utf8HtmlScanner"/> for the open-tag and
/// attribute-extraction primitives; adds close-tag location and the
/// <see cref="Heading"/> offset record. Nothing about the scan path
/// UTF-8 decodes the snapshot; existing-id strings, inner text, and
/// slugs stay as byte spans/byte arrays end-to-end.
/// </remarks>
internal static class HeadingScanner
{
    /// <summary>ASCII byte for the opening angle bracket.</summary>
    private const byte OpenAngle = (byte)'<';

    /// <summary>ASCII byte for the closing angle bracket.</summary>
    private const byte CloseAngle = (byte)'>';

    /// <summary>ASCII byte for the forward slash that begins a close tag.</summary>
    private const byte Slash = (byte)'/';

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
        while (cursor < html.Length
               && Utf8HtmlScanner.TryFindNextHeadingOpen(html, cursor, out var tagStart, out var tagEnd, out var level))
        {
            var closeIdx = FindCloseTag(html, tagEnd, level);
            if (closeIdx < 0)
            {
                cursor = tagEnd;
                continue;
            }

            var openTag = html[tagStart..tagEnd];
            var (idLocal, idLen) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
            var idAbsStart = idLen is 0 ? -1 : tagStart + idLocal;

            found.Add(new(
                Level: level,
                OpenTagStart: tagStart,
                OpenTagEnd: tagEnd,
                CloseTagStart: closeIdx,
                TextStart: tagEnd,
                TextEnd: closeIdx,
                ExistingIdStart: idAbsStart,
                ExistingIdLength: idLen,
                Slug: []));

            cursor = closeIdx + CloseTagLength;
        }

        return [.. found];
    }

    /// <summary>Streams the inner-text bytes of <paramref name="heading"/> into <paramref name="sink"/>, stripping nested tags as it goes.</summary>
    /// <param name="html">Original HTML snapshot.</param>
    /// <param name="heading">Heading record.</param>
    /// <param name="sink">UTF-8 sink (caller-managed; not touched on empty input).</param>
    /// <remarks>The output is *not* trimmed — the slugifier collapses leading/trailing whitespace as part of its rule.</remarks>
    public static void DecodeTextInto(ReadOnlySpan<byte> html, in Heading heading, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        var inner = html[heading.TextStart..heading.TextEnd];
        if (inner.IsEmpty)
        {
            return;
        }

        var runStart = 0;
        var openRel = inner.IndexOf(OpenAngle);
        while (openRel >= 0)
        {
            Utf8StringWriter.Write(sink, inner.Slice(runStart, openRel - runStart));
            var closeRel = inner[openRel..].IndexOf(CloseAngle);
            if (closeRel < 0)
            {
                return;
            }

            runStart = openRel + closeRel + 1;
            var nextRel = inner[runStart..].IndexOf(OpenAngle);
            openRel = nextRel < 0 ? -1 : runStart + nextRel;
        }

        Utf8StringWriter.Write(sink, inner[runStart..]);
    }

    /// <summary>Decodes the inner text content of <paramref name="heading"/> as a string, with whitespace trimmed.</summary>
    /// <param name="html">Original HTML snapshot.</param>
    /// <param name="heading">Heading record.</param>
    /// <returns>Plain-text body with HTML tags stripped and surrounding whitespace removed.</returns>
    /// <remarks>
    /// Convenience overload for diagnostics and tests; the production
    /// slug pipeline goes through <see cref="DecodeTextInto"/> + the
    /// byte-level slugifier and never allocates a string.
    /// </remarks>
    public static string DecodeText(ReadOnlySpan<byte> html, in Heading heading)
    {
        var inner = html[heading.TextStart..heading.TextEnd];
        if (inner.IsEmpty)
        {
            return string.Empty;
        }

        using var rental = PageBuilderPool.Rent(inner.Length);
        var buffer = rental.Writer;
        DecodeTextInto(html, in heading, buffer);
        return buffer.WrittenCount is 0
            ? string.Empty
            : Encoding.UTF8.GetString(buffer.WrittenSpan).Trim();
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
}
