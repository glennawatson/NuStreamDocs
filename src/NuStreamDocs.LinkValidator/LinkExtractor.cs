// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.LinkValidator;

/// <summary>Extracts <c>href</c> / <c>src</c> URL values and heading anchor IDs from rendered HTML as offset+length ranges.</summary>
public static class LinkExtractor
{
    /// <summary>Initial pooled-buffer capacity for attribute extraction; covers small/medium pages without growth.</summary>
    private const int InitialAttributeCapacity = 64;

    /// <summary>Initial pooled-buffer capacity for heading extraction; pages rarely have more than this many headings.</summary>
    private const int InitialHeadingCapacity = 32;

    /// <summary>Gets the UTF-8 marker introducing an <c>href=&quot;</c> attribute (with leading whitespace as the project's emitter produces).</summary>
    private static ReadOnlySpan<byte> HrefMarker => " href=\""u8;

    /// <summary>Gets the UTF-8 marker introducing a <c>src=&quot;</c> attribute.</summary>
    private static ReadOnlySpan<byte> SrcMarker => " src=\""u8;

    /// <summary>Gets the UTF-8 marker introducing an <c>id=&quot;</c> attribute on any element.</summary>
    private static ReadOnlySpan<byte> IdMarker => " id=\""u8;

    /// <summary>Gets the UTF-8 marker introducing the obsolete <c>&lt;a name=&quot;</c> fragment-target attribute.</summary>
    private static ReadOnlySpan<byte> AnchorNameMarker => "<a name=\""u8;

    /// <summary>Extracts every <c>href</c> attribute value as offset+length pairs.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Byte ranges in document order.</returns>
    public static ByteRange[] ExtractHrefRanges(ReadOnlySpan<byte> html) =>
        ExtractAttributeRanges(html, HrefMarker);

    /// <summary>Extracts every <c>src</c> attribute value as offset+length pairs.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Byte ranges in document order.</returns>
    public static ByteRange[] ExtractSrcRanges(ReadOnlySpan<byte> html) =>
        ExtractAttributeRanges(html, SrcMarker);

    /// <summary>Extracts every <c>id</c> attribute value (any element) as offset+length pairs.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Byte ranges in document order.</returns>
    public static ByteRange[] ExtractIdRanges(ReadOnlySpan<byte> html) =>
        ExtractAttributeRanges(html, IdMarker);

    /// <summary>Extracts the values of obsolete HTML4 <c>&lt;a name="..."&gt;</c> fragment anchors as offset+length pairs.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Byte ranges in document order.</returns>
    public static ByteRange[] ExtractDeprecatedNameAnchorRanges(ReadOnlySpan<byte> html) =>
        ExtractAttributeRanges(html, AnchorNameMarker);

    /// <summary>Extracts every heading <c>id</c> attribute value as offset+length pairs.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Byte ranges in document order.</returns>
    public static ByteRange[] ExtractHeadingIdRanges(ReadOnlySpan<byte> html)
    {
        if (html.IsEmpty)
        {
            return [];
        }

        var rented = ArrayPool<ByteRange>.Shared.Rent(InitialHeadingCapacity);
        try
        {
            var count = 0;
            var cursor = 0;
            while (cursor < html.Length
                   && Utf8HtmlScanner.TryFindNextHeadingOpen(html, cursor, out var tagStart, out var tagEnd, out _))
            {
                var openTag = html[tagStart..tagEnd];
                var (idLocal, idLen) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
                if (idLen > 0)
                {
                    rented = AppendGrowing(rented, count, new(tagStart + idLocal, idLen));
                    count++;
                }

                cursor = tagEnd;
            }

            return Materialize(rented, count);
        }
        finally
        {
            ArrayPool<ByteRange>.Shared.Return(rented);
        }
    }

    /// <summary>Walks <paramref name="html"/> and returns every <paramref name="marker"/> attribute value as offset+length pairs.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <param name="marker">Attribute marker (e.g. <c> href="</c>).</param>
    /// <returns>Byte ranges in document order.</returns>
    private static ByteRange[] ExtractAttributeRanges(ReadOnlySpan<byte> html, ReadOnlySpan<byte> marker)
    {
        if (html.IsEmpty)
        {
            return [];
        }

        var rented = ArrayPool<ByteRange>.Shared.Rent(InitialAttributeCapacity);
        try
        {
            var count = 0;
            var cursor = 0;
            while (cursor < html.Length)
            {
                var hit = html[cursor..].IndexOf(marker);
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

                rented = AppendGrowing(rented, count, new(valueStart, valueEnd));
                count++;
                cursor = valueStart + valueEnd + 1;
            }

            return Materialize(rented, count);
        }
        finally
        {
            ArrayPool<ByteRange>.Shared.Return(rented);
        }
    }

    /// <summary>Appends <paramref name="value"/> at <paramref name="count"/>, renting a larger pool buffer when full.</summary>
    /// <param name="buffer">Current pooled buffer.</param>
    /// <param name="count">Existing entry count (insertion index).</param>
    /// <param name="value">Range to append.</param>
    /// <returns>The same buffer (when capacity sufficed) or a freshly-rented larger buffer.</returns>
    private static ByteRange[] AppendGrowing(ByteRange[] buffer, int count, ByteRange value)
    {
        if (count >= buffer.Length)
        {
            var bigger = ArrayPool<ByteRange>.Shared.Rent(buffer.Length * 2);
            buffer.AsSpan(0, count).CopyTo(bigger);
            ArrayPool<ByteRange>.Shared.Return(buffer);
            buffer = bigger;
        }

        buffer[count] = value;
        return buffer;
    }

    /// <summary>Copies the populated prefix of <paramref name="buffer"/> into a precisely-sized result array.</summary>
    /// <param name="buffer">Pooled buffer (caller still owns + returns it).</param>
    /// <param name="count">Number of valid entries.</param>
    /// <returns>Empty when <paramref name="count"/> is zero, otherwise a fresh array sized exactly to <paramref name="count"/>.</returns>
    private static ByteRange[] Materialize(ByteRange[] buffer, int count)
    {
        if (count is 0)
        {
            return [];
        }

        var result = new ByteRange[count];
        buffer.AsSpan(0, count).CopyTo(result);
        return result;
    }
}
