// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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

        List<Heading> found = new(16);
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

            if (IsInsideAnchor(html, tagStart))
            {
                cursor = closeIdx + CloseTagLength;
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

    /// <summary>Streams the inner-text bytes of <paramref name="heading"/> into <paramref name="sink"/>, stripping nested tags and decoding HTML entities as it goes.</summary>
    /// <param name="html">Original HTML snapshot.</param>
    /// <param name="heading">Heading record.</param>
    /// <param name="sink">UTF-8 sink (caller-managed; not touched on empty input).</param>
    /// <remarks>
    /// Entities like <c>&amp;gt;</c> get decoded back to <c>&gt;</c> before the slugifier sees them so a heading
    /// like <c>## ENR -&gt; ENR</c> (rendered as <c>ENR -&amp;gt; ENR</c>) slugifies the same way as it would
    /// from raw markdown — otherwise the literal letters <c>g</c><c>t</c> would leak into the slug.
    /// The output is *not* trimmed — the slugifier collapses leading/trailing whitespace as part of its rule.
    /// </remarks>
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
            Markdown.Common.HtmlEntityDecoder.DecodeInto(sink, inner.Slice(runStart, openRel - runStart));
            var closeRel = inner[openRel..].IndexOf(CloseAngle);
            if (closeRel < 0)
            {
                return;
            }

            runStart = openRel + closeRel + 1;
            var nextRel = inner[runStart..].IndexOf(OpenAngle);
            openRel = nextRel < 0 ? -1 : runStart + nextRel;
        }

        Markdown.Common.HtmlEntityDecoder.DecodeInto(sink, inner[runStart..]);
    }

    /// <summary>Returns true when the byte at <paramref name="position"/> sits inside an open <c>&lt;a&gt;</c> element.</summary>
    /// <param name="html">Full snapshot.</param>
    /// <param name="position">Heading-tag start offset.</param>
    /// <returns>True when the most recent <c>&lt;a</c> open tag before <paramref name="position"/> has not yet been closed.</returns>
    private static bool IsInsideAnchor(ReadOnlySpan<byte> html, int position)
    {
        var lastOpen = LastIndexOfAnchorOpen(html[..position]);
        if (lastOpen < 0)
        {
            return false;
        }

        var lastClose = html[..position].LastIndexOf("</a>"u8);
        return lastClose < lastOpen;
    }

    /// <summary>Returns the index of the last <c>&lt;a</c> open-tag prefix in <paramref name="span"/>, or <c>-1</c> when none.</summary>
    /// <param name="span">Span to scan.</param>
    /// <returns>Index of the <c>&lt;</c> byte, or <c>-1</c>.</returns>
    private static int LastIndexOfAnchorOpen(ReadOnlySpan<byte> span)
    {
        // Smallest possible <a> opener is "<a>" (3 bytes); the loop indexes the '<' so we start two before the end.
        const int MinTagSpan = 3;
        for (var i = span.Length - MinTagSpan; i >= 0; i--)
        {
            const int NameOffset = 1;
            const int NameEndOffset = 2;
            if (span[i] is OpenAngle
                && span[i + NameOffset] is (byte)'a' or (byte)'A'
                && span[i + NameEndOffset] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or CloseAngle)
            {
                return i;
            }
        }

        return -1;
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
