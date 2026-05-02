// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.MdInHtml;

/// <summary>
/// Stateless UTF-8 rewriter for Markdown-in-HTML support. Walks
/// the source byte stream, locating block-level HTML opens that
/// carry the <c>markdown="1"</c> / <c>markdown="block"</c> /
/// <c>markdown="span"</c> attribute. For each match the attribute
/// is stripped and a blank line is inserted just inside the open
/// tag and just before the matching close tag so CommonMark can
/// parse the body as Markdown. Fenced-code regions pass through
/// verbatim.
/// </summary>
internal static class MdInHtmlRewriter
{
    /// <summary>Width of the trailing <c>"</c> that bounds the attribute value.</summary>
    private const int QuoteLength = 1;

    /// <summary>Length of the close-tag prefix (<c>&lt;/</c>) before the tag name.</summary>
    private const int CloseTagPrefixLength = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i) && MarkdownCodeScanner.TryConsumeFence(source, i, out var fenceEnd))
            {
                writer.Write(source[i..fenceEnd]);
                i = fenceEnd;
                continue;
            }

            if (source[i] is (byte)'<' && TryRewriteTag(source, i, writer, out var consumed))
            {
                i += consumed;
                continue;
            }

            var dest = writer.GetSpan(1);
            dest[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }

    /// <summary>Tries to match an HTML open tag carrying <c>markdown="…"</c> at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor on the leading <c>&lt;</c>.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True when a tag was rewritten.</returns>
    private static bool TryRewriteTag(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (!TryParseOpenTag(source, offset, out var name, out var openTagEnd, out var attrStart, out var attrEnd))
        {
            return false;
        }

        if (!TryFindMatchingClose(source, name, openTagEnd, out var closeStart, out var closeEnd))
        {
            return false;
        }

        // Emit: <tag attrs-without-markdown>\n\n  body  \n\n</tag>
        writer.Write(source[offset..attrStart]);
        writer.Write(source[attrEnd..openTagEnd]);
        writer.Write("\n\n"u8);
        writer.Write(source[openTagEnd..closeStart]);
        writer.Write("\n\n"u8);
        writer.Write(source[closeStart..closeEnd]);
        consumed = closeEnd - offset;
        return true;
    }

    /// <summary>Parses an open tag and locates its <c>markdown="…"</c> attribute when present.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor on the leading <c>&lt;</c>.</param>
    /// <param name="name">Captured tag name on success.</param>
    /// <param name="openTagEnd">Offset just past the closing <c>&gt;</c> on success.</param>
    /// <param name="attrStart">Inclusive start of the <c>markdown="…"</c> attribute on success (including the leading whitespace).</param>
    /// <param name="attrEnd">Exclusive end of the <c>markdown="…"</c> attribute on success.</param>
    /// <returns>True when the tag carries a recognized <c>markdown</c> attribute value.</returns>
    private static bool TryParseOpenTag(ReadOnlySpan<byte> source, int offset, out ReadOnlySpan<byte> name, out int openTagEnd, out int attrStart, out int attrEnd)
    {
        name = default;
        openTagEnd = 0;
        attrStart = 0;
        attrEnd = 0;

        // <tagname … >
        if (offset + 1 >= source.Length || !IsTagNameStart(source[offset + 1]))
        {
            return false;
        }

        var nameStart = offset + 1;
        var nameEnd = nameStart;
        while (nameEnd < source.Length && IsTagNameByte(source[nameEnd]))
        {
            nameEnd++;
        }

        var closeRel = source[nameEnd..].IndexOf((byte)'>');
        if (closeRel < 0)
        {
            return false;
        }

        openTagEnd = nameEnd + closeRel + 1;
        var attrSpan = source[nameEnd..(openTagEnd - 1)];
        if (!TryFindMarkdownAttribute(attrSpan, out var relStart, out var relEnd))
        {
            return false;
        }

        attrStart = nameEnd + relStart;
        attrEnd = nameEnd + relEnd;
        name = source[nameStart..nameEnd];
        return true;
    }

    /// <summary>Locates a <c>markdown="…"</c> attribute inside the attribute area of an open tag.</summary>
    /// <param name="attrs">Attribute span (the bytes between the tag name and the closing <c>&gt;</c>).</param>
    /// <param name="relStart">Relative start of the attribute (including the leading whitespace).</param>
    /// <param name="relEnd">Relative end of the attribute (just past the closing quote).</param>
    /// <returns>True when an attribute was found.</returns>
    private static bool TryFindMarkdownAttribute(ReadOnlySpan<byte> attrs, out int relStart, out int relEnd)
    {
        relStart = 0;
        relEnd = 0;
        var needle = "markdown="u8;
        for (var i = 0; i < attrs.Length - needle.Length; i++)
        {
            if (!IsAttributeBoundary(attrs, i))
            {
                continue;
            }

            var afterWhitespace = i + 1;
            if (!attrs[afterWhitespace..].StartsWith(needle))
            {
                continue;
            }

            var quoteStart = afterWhitespace + needle.Length;
            if (quoteStart >= attrs.Length || attrs[quoteStart] is not (byte)'"')
            {
                continue;
            }

            var valueStart = quoteStart + QuoteLength;
            var valueRel = attrs[valueStart..].IndexOf((byte)'"');
            if (valueRel < 0)
            {
                continue;
            }

            var value = attrs.Slice(valueStart, valueRel);
            if (!IsRecognizedMarkdownValue(value))
            {
                continue;
            }

            relStart = i;
            relEnd = valueStart + valueRel + QuoteLength;
            return true;
        }

        return false;
    }

    /// <summary>Returns true when the byte at <paramref name="offset"/> is whitespace separating attributes.</summary>
    /// <param name="span">Attribute span.</param>
    /// <param name="offset">Position to test.</param>
    /// <returns>True for an attribute-separator boundary.</returns>
    private static bool IsAttributeBoundary(ReadOnlySpan<byte> span, int offset) =>
        span[offset] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n';

    /// <summary>Returns true when <paramref name="value"/> is one of the recognized <c>markdown</c> attribute values.</summary>
    /// <param name="value">Attribute value bytes.</param>
    /// <returns>True for <c>1</c>, <c>block</c>, or <c>span</c>.</returns>
    private static bool IsRecognizedMarkdownValue(ReadOnlySpan<byte> value) =>
        value.SequenceEqual("1"u8)
        || value.SequenceEqual("block"u8)
        || value.SequenceEqual("span"u8);

    /// <summary>Finds the matching close tag for <paramref name="name"/>, accounting for nested same-name tags.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="name">Tag name to match (case-sensitive — HTML5 normalization isn't required for the conservative blocks we handle).</param>
    /// <param name="from">Search-start offset (just past the open tag's <c>&gt;</c>).</param>
    /// <param name="closeStart">Offset of the close tag's leading <c>&lt;</c>.</param>
    /// <param name="closeEnd">Offset just past the close tag's <c>&gt;</c>.</param>
    /// <returns>True when a matching close tag was found.</returns>
    private static bool TryFindMatchingClose(ReadOnlySpan<byte> source, ReadOnlySpan<byte> name, int from, out int closeStart, out int closeEnd)
    {
        closeStart = 0;
        closeEnd = 0;
        var depth = 1;
        var p = from;
        while (p < source.Length)
        {
            var rel = source[p..].IndexOf((byte)'<');
            if (rel < 0)
            {
                return false;
            }

            var tagStart = p + rel;
            if (TryMatchOpen(source, tagStart, name, out var openEnd))
            {
                depth++;
                p = openEnd;
                continue;
            }

            if (TryMatchClose(source, tagStart, name, out var thisCloseEnd))
            {
                depth--;
                if (depth is 0)
                {
                    closeStart = tagStart;
                    closeEnd = thisCloseEnd;
                    return true;
                }

                p = thisCloseEnd;
                continue;
            }

            p = tagStart + 1;
        }

        return false;
    }

    /// <summary>Returns true when the bytes at <paramref name="offset"/> form an open tag with the given <paramref name="name"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the <c>&lt;</c>.</param>
    /// <param name="name">Tag name.</param>
    /// <param name="openEnd">Offset just past the matching <c>&gt;</c>.</param>
    /// <returns>True on match.</returns>
    private static bool TryMatchOpen(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> name, out int openEnd)
    {
        openEnd = 0;
        if (offset + 1 + name.Length >= source.Length
            || source[offset + 1] is (byte)'/'
            || !source[(offset + 1)..].StartsWith(name))
        {
            return false;
        }

        var afterName = offset + 1 + name.Length;
        if (afterName >= source.Length || (source[afterName] is not (byte)'>' and not (byte)' ' and not (byte)'\t' and not (byte)'\n'))
        {
            return false;
        }

        var rel = source[afterName..].IndexOf((byte)'>');
        if (rel < 0)
        {
            return false;
        }

        openEnd = afterName + rel + 1;
        return true;
    }

    /// <summary>Returns true when the bytes at <paramref name="offset"/> form a close tag with the given <paramref name="name"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the <c>&lt;</c>.</param>
    /// <param name="name">Tag name.</param>
    /// <param name="closeEnd">Offset just past the matching <c>&gt;</c>.</param>
    /// <returns>True on match.</returns>
    private static bool TryMatchClose(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> name, out int closeEnd)
    {
        closeEnd = 0;
        if (offset + CloseTagPrefixLength + name.Length >= source.Length
            || source[offset + 1] is not (byte)'/'
            || !source[(offset + CloseTagPrefixLength)..].StartsWith(name))
        {
            return false;
        }

        var afterName = offset + CloseTagPrefixLength + name.Length;
        var rel = source[afterName..].IndexOf((byte)'>');
        if (rel < 0 || HasNonWhitespaceBetween(source, afterName, afterName + rel))
        {
            return false;
        }

        closeEnd = afterName + rel + 1;
        return true;
    }

    /// <summary>Returns true when <paramref name="source"/>[<paramref name="from"/>..<paramref name="to"/>] contains a non-whitespace byte.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="from">Inclusive start.</param>
    /// <param name="to">Exclusive end.</param>
    /// <returns>True when non-whitespace is present.</returns>
    private static bool HasNonWhitespaceBetween(ReadOnlySpan<byte> source, int from, int to)
    {
        for (var i = from; i < to; i++)
        {
            if (source[i] is not ((byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n'))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns true when <paramref name="b"/> can start an HTML tag name.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for an ASCII letter.</returns>
    private static bool IsTagNameStart(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z';

    /// <summary>Returns true when <paramref name="b"/> can continue an HTML tag name.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for letters, digits, <c>-</c>, or <c>_</c>.</returns>
    private static bool IsTagNameByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'-' or (byte)'_';
}
