// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Toc;

/// <summary>Locates <c>&lt;h1&gt;</c>..<c>&lt;h6&gt;</c> open/close tag pairs in a UTF-8 HTML snapshot.</summary>
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
        var anchorDepth = 0;
        var anchorCursor = 0;
        while (cursor < html.Length
               && Utf8HtmlScanner.TryFindNextHeadingOpen(html, cursor, out var tagStart, out var tagEnd, out var level))
        {
            // Single linear sweep across the bytes we just skipped so we know whether the
            // heading sits inside an open <a> without re-scanning the prefix per heading.
            anchorDepth = AdvanceAnchorDepth(html, anchorCursor, tagStart, anchorDepth);
            anchorCursor = tagStart;

            var closeIdx = FindCloseTag(html, tagEnd, level);
            if (closeIdx < 0)
            {
                cursor = tagEnd;
                continue;
            }

            if (anchorDepth > 0)
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

    /// <summary>Streams <paramref name="heading"/>'s inner-text bytes into <paramref name="sink"/>, stripping nested tags and decoding HTML entities. Output is not trimmed.</summary>
    /// <param name="html">Original HTML snapshot.</param>
    /// <param name="heading">Heading record.</param>
    /// <param name="sink">UTF-8 sink.</param>
    public static void DecodeTextInto(ReadOnlySpan<byte> html, in Heading heading, IBufferWriter<byte> sink)
    {
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

    /// <summary>
    /// Walks <paramref name="html"/> from <paramref name="from"/> to <paramref name="to"/> applying every
    /// <c>&lt;a&gt;</c> open and <c>&lt;/a&gt;</c> close to <paramref name="depth"/>.
    /// </summary>
    /// <param name="html">Full snapshot.</param>
    /// <param name="from">Inclusive start offset (where the previous walk left off).</param>
    /// <param name="to">Exclusive end offset (current heading-tag start).</param>
    /// <param name="depth">Current open-anchor depth before the walk.</param>
    /// <returns>Updated open-anchor depth after consuming the slice.</returns>
    private static int AdvanceAnchorDepth(ReadOnlySpan<byte> html, int from, int to, int depth)
    {
        if (from >= to)
        {
            return depth;
        }

        var slice = html[from..to];
        var p = 0;
        while (p < slice.Length)
        {
            var rel = slice[p..].IndexOf(OpenAngle);
            if (rel < 0)
            {
                break;
            }

            var pos = p + rel;
            depth = ApplyAnchorTagAt(slice, pos, depth);
            p = pos + 1;
        }

        return depth;
    }

    /// <summary>Applies a single anchor open/close at <paramref name="pos"/> to <paramref name="depth"/>.</summary>
    /// <param name="slice">Bytes being scanned.</param>
    /// <param name="pos">Offset of the candidate <c>&lt;</c> byte.</param>
    /// <param name="depth">Current open-anchor depth.</param>
    /// <returns>Updated depth (incremented on <c>&lt;a&gt;</c>/<c>&lt;a …&gt;</c>, decremented on <c>&lt;/a&gt;</c>).</returns>
    private static int ApplyAnchorTagAt(ReadOnlySpan<byte> slice, int pos, int depth)
    {
        if (IsAnchorClose(slice, pos))
        {
            return depth > 0 ? depth - 1 : 0;
        }

        return IsAnchorOpen(slice, pos) ? depth + 1 : depth;
    }

    /// <summary>True when <paramref name="pos"/> in <paramref name="slice"/> begins a <c>&lt;/a&gt;</c> close tag.</summary>
    /// <param name="slice">Bytes being scanned.</param>
    /// <param name="pos">Offset of the <c>&lt;</c> byte.</param>
    /// <returns>True for a close-anchor tag.</returns>
    private static bool IsAnchorClose(ReadOnlySpan<byte> slice, int pos)
    {
        const int SlashOffset = 1;
        const int NameOffset = 2;
        const int GtOffset = 3;
        return pos + GtOffset < slice.Length
            && slice[pos + SlashOffset] is Slash
            && slice[pos + NameOffset] is (byte)'a' or (byte)'A'
            && slice[pos + GtOffset] is CloseAngle;
    }

    /// <summary>True when <paramref name="pos"/> in <paramref name="slice"/> begins a <c>&lt;a&gt;</c> or <c>&lt;a …</c> open tag.</summary>
    /// <param name="slice">Bytes being scanned.</param>
    /// <param name="pos">Offset of the <c>&lt;</c> byte.</param>
    /// <returns>True for an open-anchor tag.</returns>
    private static bool IsAnchorOpen(ReadOnlySpan<byte> slice, int pos)
    {
        const int NameOffset = 1;
        const int TerminatorOffset = 2;
        return pos + TerminatorOffset < slice.Length
            && slice[pos + NameOffset] is (byte)'a' or (byte)'A'
            && slice[pos + TerminatorOffset] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or CloseAngle;
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
