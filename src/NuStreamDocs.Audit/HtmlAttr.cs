// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Audit;

/// <summary>Parses HTML attribute name/value pairs out of the text between a tag name and its closing bracket.</summary>
internal static class HtmlAttr
{
    /// <summary>Looks up the value of <paramref name="name"/> (ASCII case-insensitive) within an attribute text run.</summary>
    /// <param name="attributes">The bytes between the tag name and the closing <c>&gt;</c> (or <c>/&gt;</c>).</param>
    /// <param name="name">Attribute name to find.</param>
    /// <param name="value">On success, the unquoted value bytes; empty for a bare attribute or <c>name=""</c>.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    public static bool TryGet(ReadOnlySpan<byte> attributes, ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value)
    {
        var i = 0;
        while (i < attributes.Length)
        {
            i = ParseAttribute(attributes, i, out var attrName, out var attrValue);
            if (attrName is [_, ..] && AsciiByteHelpers.EqualsIgnoreAsciiCase(attrName, name))
            {
                value = attrValue;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>Tests whether <paramref name="name"/> is present in an attribute text run.</summary>
    /// <param name="attributes">The bytes between the tag name and the closing <c>&gt;</c>.</param>
    /// <param name="name">Attribute name to find.</param>
    /// <returns><see langword="true"/> when the attribute is present.</returns>
    public static bool Has(ReadOnlySpan<byte> attributes, ReadOnlySpan<byte> name) =>
        TryGet(attributes, name, out _);

    /// <summary>Parses a single attribute starting at <paramref name="start"/> and returns the offset just past it.</summary>
    /// <param name="attributes">Attribute text run.</param>
    /// <param name="start">Offset to begin parsing at.</param>
    /// <param name="name">On return, the attribute name bytes (empty when only whitespace / separators were skipped).</param>
    /// <param name="value">On return, the unquoted attribute value bytes (empty for a bare attribute).</param>
    /// <returns>The offset of the next attribute, or <see cref="ReadOnlySpan{T}.Length"/> at the end.</returns>
    private static int ParseAttribute(
        ReadOnlySpan<byte> attributes,
        int start,
        out ReadOnlySpan<byte> name,
        out ReadOnlySpan<byte> value)
    {
        name = default;
        value = default;

        var i = SkipToAttributeStart(attributes, start);
        if (i >= attributes.Length)
        {
            return attributes.Length;
        }

        var nameStart = i;
        while (i < attributes.Length
               && !AsciiByteHelpers.IsAsciiWhitespace(attributes[i])
               && attributes[i] is not ((byte)'=' or (byte)'/'))
        {
            i++;
        }

        name = attributes[nameStart..i];
        i = AsciiByteHelpers.SkipWhitespace(attributes, i);
        if (i >= attributes.Length || attributes[i] != (byte)'=')
        {
            return i;
        }

        i = AsciiByteHelpers.SkipWhitespace(attributes, i + 1);
        return ParseValue(attributes, i, out value);
    }

    /// <summary>Skips leading whitespace and stray <c>/</c> / <c>=</c> bytes to the start of the next attribute name.</summary>
    /// <param name="attributes">Attribute text run.</param>
    /// <param name="start">Offset to begin at.</param>
    /// <returns>The offset of the next attribute name, or <see cref="ReadOnlySpan{T}.Length"/> at the end.</returns>
    private static int SkipToAttributeStart(ReadOnlySpan<byte> attributes, int start)
    {
        var i = AsciiByteHelpers.SkipWhitespace(attributes, start);
        while (i < attributes.Length && attributes[i] is (byte)'/' or (byte)'=')
        {
            i = AsciiByteHelpers.SkipWhitespace(attributes, i + 1);
        }

        return i;
    }

    /// <summary>Reads an attribute value (quoted or unquoted) starting at <paramref name="start"/>.</summary>
    /// <param name="attributes">Attribute text run.</param>
    /// <param name="start">Offset of the first value byte (or quote).</param>
    /// <param name="value">On return, the unquoted value bytes.</param>
    /// <returns>The offset just past the value.</returns>
    private static int ParseValue(ReadOnlySpan<byte> attributes, int start, out ReadOnlySpan<byte> value)
    {
        if (start >= attributes.Length)
        {
            value = default;
            return start;
        }

        var quote = attributes[start];
        if (quote is (byte)'"' or (byte)'\'')
        {
            var valueStart = start + 1;
            var end = attributes[valueStart..].IndexOf(quote);
            if (end < 0)
            {
                value = attributes[valueStart..];
                return attributes.Length;
            }

            value = attributes.Slice(valueStart, end);
            return valueStart + end + 1;
        }

        var unquotedStart = start;
        var i = start;
        while (i < attributes.Length && !AsciiByteHelpers.IsAsciiWhitespace(attributes[i]))
        {
            i++;
        }

        value = attributes[unquotedStart..i];
        return i;
    }
}
