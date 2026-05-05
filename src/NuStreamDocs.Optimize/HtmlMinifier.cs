// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Optimize;

/// <summary>
/// Byte-level UTF-8 HTML minifier. Collapses inter-tag whitespace, strips
/// comments, and copies the contents of <c>&lt;pre&gt;</c>, <c>&lt;code&gt;</c>,
/// <c>&lt;textarea&gt;</c>, <c>&lt;script&gt;</c>, and <c>&lt;style&gt;</c>
/// blocks verbatim so source samples and JS/CSS are preserved.
/// </summary>
internal static class HtmlMinifier
{
    /// <summary>Length of the <c>&lt;!--</c> comment opener.</summary>
    private const int CommentOpenLength = 4;

    /// <summary>Length of the <c>--&gt;</c> comment closer.</summary>
    private const int CommentCloseLength = 3;

    /// <summary>Length of the <c>&lt;/</c> close-tag prefix.</summary>
    private const int CloseTagPrefixLength = 2;

    /// <summary>Offset between an ASCII uppercase letter and its lowercase counterpart.</summary>
    private const int AsciiUpperToLowerOffset = 32;

    /// <summary>Whitespace bytes recognized between tags.</summary>
    private static readonly SearchValues<byte> Whitespace = SearchValues.Create(" \t\r\n"u8);

    /// <summary>Lower-case tag names whose body must be passed through verbatim.</summary>
    private static readonly byte[][] PreserveTags =
    [
        [.. "pre"u8],
        [.. "code"u8],
        [.. "textarea"u8],
        [.. "script"u8],
        [.. "style"u8]
    ];

    /// <summary>Minifies <paramref name="source"/> into <paramref name="writer"/> in a single forward pass.</summary>
    /// <param name="source">UTF-8 HTML bytes.</param>
    /// <param name="writer">Destination buffer writer.</param>
    /// <param name="options">Minify options.</param>
    public static void Minify(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, in HtmlMinifyOptions options)
    {
        var cursor = 0;
        var pendingSpace = false;
        var afterTag = true;
        while (cursor < source.Length)
        {
            if (source[cursor] is (byte)'<')
            {
                cursor = HandleAngleBracket(source, cursor, writer, options, ref pendingSpace);
                afterTag = true;
                continue;
            }

            cursor = HandleText(source, cursor, writer, options, ref pendingSpace, ref afterTag);
        }
    }

    /// <summary>Handles a non-tag byte (text or whitespace).</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Cursor offset.</param>
    /// <param name="writer">Destination.</param>
    /// <param name="options">Options.</param>
    /// <param name="pendingSpace">Whether a single collapsed space is pending emission.</param>
    /// <param name="afterTag">Whether the previous emission ended with a tag.</param>
    /// <returns>New cursor offset.</returns>
    private static int HandleText(ReadOnlySpan<byte> source, int cursor, IBufferWriter<byte> writer, in HtmlMinifyOptions options, ref bool pendingSpace, ref bool afterTag)
    {
        var b = source[cursor];
        if (options.CollapseWhitespace && Whitespace.Contains(b))
        {
            if (!afterTag)
            {
                pendingSpace = true;
            }

            return cursor + 1;
        }

        if (pendingSpace)
        {
            WriteByte(writer, (byte)' ');
            pendingSpace = false;
        }

        WriteByte(writer, b);
        afterTag = false;
        return cursor + 1;
    }

    /// <summary>Dispatches a <c>&lt;</c> to comment, preserve-block, or regular-tag handlers.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor offset of the <c>&lt;</c>.</param>
    /// <param name="writer">Destination.</param>
    /// <param name="options">Options.</param>
    /// <param name="pendingSpace">Whether a collapsed space was pending; cleared on tag emission.</param>
    /// <returns>Cursor positioned just past the handled construct.</returns>
    private static int HandleAngleBracket(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, in HtmlMinifyOptions options, ref bool pendingSpace)
    {
        if (options.StripComments && StartsWith(source, offset, "<!--"u8))
        {
            return SkipComment(source, offset + CommentOpenLength);
        }

        var preserveLen = MatchPreserveTag(source, offset);
        if (preserveLen > 0)
        {
            pendingSpace = false;
            return CopyPreserveBlock(source, offset, preserveLen, writer);
        }

        pendingSpace = false;
        return CopyTag(source, offset, writer);
    }

    /// <summary>Determines whether <paramref name="source"/> at <paramref name="offset"/> starts with <paramref name="prefix"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="prefix">Bytes to compare.</param>
    /// <returns>True on a prefix match.</returns>
    private static bool StartsWith(ReadOnlySpan<byte> source, int offset, ReadOnlySpan<byte> prefix) =>
        offset + prefix.Length <= source.Length && source.Slice(offset, prefix.Length).SequenceEqual(prefix);

    /// <summary>Returns the length of a preserve-tag opener (e.g. <c>&lt;pre&gt;</c>) starting at <paramref name="offset"/>, or 0.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Offset of the leading <c>&lt;</c>.</param>
    /// <returns>Length in bytes of the matched <c>tag</c> name (without the <c>&lt;</c>), or 0 when no preserve tag matches.</returns>
    private static int MatchPreserveTag(ReadOnlySpan<byte> source, int offset)
    {
        for (var i = 0; i < PreserveTags.Length; i++)
        {
            var name = PreserveTags[i];
            var nameStart = offset + 1;
            var end = nameStart + name.Length;
            if (end > source.Length)
            {
                continue;
            }

            if (!EqualsAsciiCaseInsensitive(source.Slice(nameStart, name.Length), name))
            {
                continue;
            }

            if (end < source.Length && !IsTagBoundary(source[end]))
            {
                continue;
            }

            return name.Length;
        }

        return 0;
    }

    /// <summary>Returns true when <paramref name="b"/> can terminate a tag name (whitespace, <c>/</c>, or <c>&gt;</c>).</summary>
    /// <param name="b">Source byte.</param>
    /// <returns>True when the byte is a tag-name terminator.</returns>
    private static bool IsTagBoundary(byte b) => b is (byte)'>' or (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'\r' or (byte)'/';

    /// <summary>Compares two ASCII byte spans case-insensitively.</summary>
    /// <param name="a">First span.</param>
    /// <param name="b">Second span.</param>
    /// <returns>True when equal ignoring ASCII case.</returns>
    private static bool EqualsAsciiCaseInsensitive(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (ToLowerAscii(a[i]) != ToLowerAscii(b[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>ASCII lower-case fold.</summary>
    /// <param name="b">Source byte.</param>
    /// <returns>Lower-cased byte for ASCII letters; passthrough otherwise.</returns>
    private static byte ToLowerAscii(byte b) => b is >= (byte)'A' and <= (byte)'Z' ? (byte)(b + AsciiUpperToLowerOffset) : b;

    /// <summary>Copies a preserve-tag block (open tag through matching close tag) verbatim into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Offset of the leading <c>&lt;</c>.</param>
    /// <param name="nameLength">Length of the preserve-tag name.</param>
    /// <param name="writer">Destination.</param>
    /// <returns>Cursor positioned just past the closing tag, or source-end on truncation.</returns>
    private static int CopyPreserveBlock(ReadOnlySpan<byte> source, int offset, int nameLength, IBufferWriter<byte> writer)
    {
        var name = PreserveTags[PreserveTagIndex(nameLength)];
        var closeAt = FindCloseTag(source, offset + 1 + nameLength, name);
        if (closeAt < 0)
        {
            Write(writer, source[offset..]);
            return source.Length;
        }

        var endOfClose = SkipUntil(source, closeAt, (byte)'>') + 1;
        Write(writer, source[offset..endOfClose]);
        return endOfClose;
    }

    /// <summary>Finds the offset of the matching <c>&lt;/name&gt;</c> close tag at or after <paramref name="cursor"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Search start.</param>
    /// <param name="name">Lower-case tag name.</param>
    /// <returns>Offset of <c>&lt;</c> on the close tag, or -1 when truncated.</returns>
    private static int FindCloseTag(ReadOnlySpan<byte> source, int cursor, ReadOnlySpan<byte> name)
    {
        while (cursor < source.Length)
        {
            var rel = source[cursor..].IndexOf((byte)'<');
            if (rel < 0)
            {
                return -1;
            }

            cursor += rel;
            if (IsCloseTagAt(source, cursor, name))
            {
                return cursor;
            }

            cursor++;
        }

        return -1;
    }

    /// <summary>Determines whether the bytes at <paramref name="cursor"/> form <c>&lt;/name&gt;</c> with a tag-boundary terminator.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Cursor offset (must point at <c>&lt;</c>).</param>
    /// <param name="name">Lower-case tag name.</param>
    /// <returns>True when the cursor is positioned at a matching close tag.</returns>
    private static bool IsCloseTagAt(ReadOnlySpan<byte> source, int cursor, ReadOnlySpan<byte> name)
    {
        if (!StartsWith(source, cursor, "</"u8))
        {
            return false;
        }

        var nameStart = cursor + CloseTagPrefixLength;
        var end = nameStart + name.Length;
        if (end > source.Length)
        {
            return false;
        }

        return EqualsAsciiCaseInsensitive(source.Slice(nameStart, name.Length), name)
            && (end == source.Length || IsTagBoundary(source[end]));
    }

    /// <summary>Returns the index in <see cref="PreserveTags"/> matching a known length.</summary>
    /// <param name="length">Length of the preserve-tag name in bytes.</param>
    /// <returns>Index into <see cref="PreserveTags"/>.</returns>
    private static int PreserveTagIndex(int length)
    {
        for (var i = 0; i < PreserveTags.Length; i++)
        {
            if (PreserveTags[i].Length == length)
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>Copies a regular tag (open or close, leading <c>&lt;</c> through trailing <c>&gt;</c>) verbatim.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Offset of <c>&lt;</c>.</param>
    /// <param name="writer">Destination.</param>
    /// <returns>Cursor positioned just past <c>&gt;</c>.</returns>
    private static int CopyTag(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer)
    {
        var end = SkipUntil(source, offset, (byte)'>');
        if (end >= source.Length)
        {
            Write(writer, source[offset..]);
            return source.Length;
        }

        Write(writer, source[offset..(end + 1)]);
        return end + 1;
    }

    /// <summary>Returns the index of <paramref name="needle"/> at or after <paramref name="offset"/>, or source-end when not found.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="needle">Byte to find.</param>
    /// <returns>Absolute offset.</returns>
    private static int SkipUntil(ReadOnlySpan<byte> source, int offset, byte needle)
    {
        var rel = source[offset..].IndexOf(needle);
        return rel < 0 ? source.Length : offset + rel;
    }

    /// <summary>Returns the offset just past <c>--&gt;</c>, or source-end when truncated.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor (just past <c>&lt;!--</c>).</param>
    /// <returns>Cursor positioned after <c>--&gt;</c>.</returns>
    private static int SkipComment(ReadOnlySpan<byte> source, int offset)
    {
        var rel = source[offset..].IndexOf("-->"u8);
        return rel < 0 ? source.Length : offset + rel + CommentCloseLength;
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to write.</param>
    private static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Writes a single byte to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="b">Byte.</param>
    private static void WriteByte(IBufferWriter<byte> writer, byte b)
    {
        var dst = writer.GetSpan(1);
        dst[0] = b;
        writer.Advance(1);
    }
}
