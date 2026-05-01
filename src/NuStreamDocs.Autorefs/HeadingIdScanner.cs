// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Scans rendered HTML bytes for heading <c>id="..."</c> attributes and
/// publishes them to an <see cref="AutorefsRegistry"/>.
/// </summary>
/// <remarks>
/// The walker is byte-level: looks for <c>&lt;h</c> followed by a digit
/// 1–6 and a space, then for the next <c>id="..."</c> within the open
/// tag. No HTML parser; the renderer's own emitter is the one
/// authoritative source of these tags so the shape is stable.
/// </remarks>
public static class HeadingIdScanner
{
    /// <summary>Bytes the heading-open scanner must look ahead past <c>&lt;</c>: one for <c>h</c>, one for the level digit.</summary>
    private const int HeadingTagLookahead = 2;

    /// <summary>Scans <paramref name="html"/> and registers every heading ID it finds.</summary>
    /// <param name="html">UTF-8 rendered HTML bytes.</param>
    /// <param name="pageRelativeUrl">URL the page will be served at, relative to the site root.</param>
    /// <param name="registry">Registry to publish into.</param>
    public static void ScanAndRegister(ReadOnlySpan<byte> html, string pageRelativeUrl, AutorefsRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrEmpty(pageRelativeUrl);

        var cursor = 0;
        while (cursor < html.Length)
        {
            var headingStart = FindHeadingOpen(html, cursor);
            if (headingStart < 0)
            {
                return;
            }

            var tagEnd = html[headingStart..].IndexOf((byte)'>');
            if (tagEnd < 0)
            {
                return;
            }

            var openTag = html.Slice(headingStart, tagEnd);
            if (TryExtractId(openTag, out var id))
            {
                registry.Register(id, pageRelativeUrl, id);
            }

            cursor = headingStart + tagEnd + 1;
        }
    }

    /// <summary>Finds the next byte index of a heading open-tag (<c>&lt;h1</c>..<c>&lt;h6</c>).</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="from">Starting byte offset.</param>
    /// <returns>Index of the <c>&lt;</c>, or -1 when no further heading is present.</returns>
    private static int FindHeadingOpen(ReadOnlySpan<byte> html, int from)
    {
        // Vectorised: hop between '<' bytes (SIMD-backed IndexOf) and only
        // pay the lookahead check at candidate sites. On rendered HTML pages
        // — most bytes of which are text content between tags — this drops
        // the inner-loop body count by a large factor versus a per-byte walk.
        var cursor = from;
        while (cursor + HeadingTagLookahead < html.Length)
        {
            var rel = html[cursor..].IndexOf((byte)'<');
            if (rel < 0)
            {
                return -1;
            }

            var i = cursor + rel;
            if (i + HeadingTagLookahead >= html.Length)
            {
                return -1;
            }

            if (html[i + 1] is not ((byte)'h' or (byte)'H'))
            {
                cursor = i + 1;
                continue;
            }

            var level = html[i + 2];
            if (level is < (byte)'1' or > (byte)'6')
            {
                cursor = i + 1;
                continue;
            }

            return i;
        }

        return -1;
    }

    /// <summary>Extracts the <c>id="..."</c> attribute value from a tag's contents.</summary>
    /// <param name="openTag">The bytes between the leading <c>&lt;</c> and the closing <c>&gt;</c>.</param>
    /// <param name="id">Decoded id on success.</param>
    /// <returns>True when an id attribute was present.</returns>
    private static bool TryExtractId(ReadOnlySpan<byte> openTag, out string id)
    {
        var idMarker = " id=\""u8;
        var pos = openTag.IndexOf(idMarker);
        if (pos < 0)
        {
            id = string.Empty;
            return false;
        }

        var valueStart = pos + idMarker.Length;
        var valueEnd = openTag[valueStart..].IndexOf((byte)'"');
        if (valueEnd <= 0)
        {
            id = string.Empty;
            return false;
        }

        id = System.Text.Encoding.UTF8.GetString(openTag.Slice(valueStart, valueEnd));
        return true;
    }
}
