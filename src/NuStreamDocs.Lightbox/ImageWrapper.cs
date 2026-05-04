// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Lightbox;

/// <summary>
/// Streams an HTML rewrite that wraps every standalone <c>&lt;img&gt;</c>
/// in a glightbox anchor.
/// </summary>
/// <remarks>
/// Byte-level walker; no HTML parser. Skips images that are already
/// children of an open <c>&lt;a&gt;</c> tag — they typically already
/// link somewhere meaningful (CTA, internal nav) and shouldn't be
/// hijacked into a lightbox.
/// </remarks>
public static class ImageWrapper
{
    /// <summary>Rewrites <paramref name="source"/> into <paramref name="sink"/>, wrapping standalone images.</summary>
    /// <param name="source">UTF-8 HTML.</param>
    /// <param name="selector">UTF-8 class name applied to the wrapping anchor.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>The number of images that were wrapped.</returns>
    public static int Rewrite(ReadOnlySpan<byte> source, ReadOnlySpan<byte> selector, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (selector.IsEmpty)
        {
            throw new ArgumentException("Selector must be non-empty.", nameof(selector));
        }

        var wrapped = 0;
        var anchorDepth = 0;
        var cursor = 0;
        while (cursor < source.Length)
        {
            var rest = source[cursor..];
            var lt = rest.IndexOf((byte)'<');
            if (lt < 0)
            {
                Write(sink, rest);
                break;
            }

            Write(sink, rest[..lt]);
            var tagStart = cursor + lt;
            var rel = source[tagStart..];

            UpdateAnchorDepth(rel, ref anchorDepth);

            if (anchorDepth == 0 && StartsWith(rel, "<img "u8)
                && TryWrapImage(source, tagStart, selector, sink, out var afterTag))
            {
                cursor = afterTag;
                wrapped++;
                continue;
            }

            // Tag we're not transforming — emit verbatim up to and including the closing '>'.
            var endIdx = FindTagEnd(source, tagStart);
            Write(sink, source[tagStart..endIdx]);
            cursor = endIdx;
        }

        return wrapped;
    }

    /// <summary>Adjusts <paramref name="anchorDepth"/> when <paramref name="tag"/> opens or closes an <c>&lt;a&gt;</c>.</summary>
    /// <param name="tag">Source slice starting at <c>&lt;</c>.</param>
    /// <param name="anchorDepth">Open-anchor counter; never decremented below zero.</param>
    private static void UpdateAnchorDepth(ReadOnlySpan<byte> tag, ref int anchorDepth)
    {
        if (StartsWith(tag, "<a "u8) || StartsWith(tag, "<a>"u8))
        {
            anchorDepth++;
            return;
        }

        if (!StartsWith(tag, "</a>"u8) || anchorDepth <= 0)
        {
            return;
        }

        anchorDepth--;
    }

    /// <summary>Emits a glightbox-anchor wrap for the <c>&lt;img&gt;</c> tag at <paramref name="tagStart"/>.</summary>
    /// <param name="source">Full UTF-8 source.</param>
    /// <param name="tagStart">Index of the leading <c>&lt;</c>.</param>
    /// <param name="selector">UTF-8 class name applied to the wrapping anchor.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="afterTag">Index just past the closing <c>&gt;</c> when wrapping succeeded.</param>
    /// <returns>True when the image was wrapped; false leaves the caller to emit the tag verbatim.</returns>
    private static bool TryWrapImage(
        ReadOnlySpan<byte> source,
        int tagStart,
        ReadOnlySpan<byte> selector,
        IBufferWriter<byte> sink,
        out int afterTag)
    {
        afterTag = FindTagEnd(source, tagStart);
        if (!TryGetSrcRange(source[tagStart..afterTag], out var srcStart, out var srcLength))
        {
            return false;
        }

        Write(sink, "<a href=\""u8);
        Write(sink, source.Slice(tagStart + srcStart, srcLength));
        Write(sink, "\" class=\""u8);
        Write(sink, selector);
        Write(sink, "\">"u8);
        Write(sink, source[tagStart..afterTag]);
        Write(sink, "</a>"u8);
        return true;
    }

    /// <summary>Returns the index just past the next <c>&gt;</c> in <paramref name="source"/> from <paramref name="start"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="start">Tag-open index.</param>
    /// <returns>One past the closing angle, or the source length if none.</returns>
    private static int FindTagEnd(ReadOnlySpan<byte> source, int start)
    {
        var rest = source[start..];
        var hit = rest.IndexOf((byte)'>');
        return hit < 0 ? source.Length : start + hit + 1;
    }

    /// <summary>True when <paramref name="value"/> begins with <paramref name="prefix"/>.</summary>
    /// <param name="value">UTF-8 value.</param>
    /// <param name="prefix">UTF-8 prefix.</param>
    /// <returns>True on match.</returns>
    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) =>
        value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);

    /// <summary>Locates the <c>src="..."</c> attribute value inside an <c>&lt;img&gt;</c> tag and returns its byte range relative to the tag.</summary>
    /// <param name="tag">Bytes from the leading <c>&lt;</c> through to the closing <c>&gt;</c>.</param>
    /// <param name="valueStart">Tag-relative byte offset of the first src character on success.</param>
    /// <param name="valueLength">Length of the src value in bytes on success.</param>
    /// <returns>True when an src attribute was found.</returns>
    private static bool TryGetSrcRange(ReadOnlySpan<byte> tag, out int valueStart, out int valueLength)
    {
        var marker = " src=\""u8;
        var pos = tag.IndexOf(marker);
        if (pos < 0)
        {
            valueStart = 0;
            valueLength = 0;
            return false;
        }

        var start = pos + marker.Length;
        var len = tag[start..].IndexOf((byte)'"');
        if (len <= 0)
        {
            valueStart = 0;
            valueLength = 0;
            return false;
        }

        valueStart = start;
        valueLength = len;
        return true;
    }

    /// <summary>Bulk write of UTF-8 bytes into <paramref name="sink"/>.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> sink, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = sink.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        sink.Advance(bytes.Length);
    }
}
