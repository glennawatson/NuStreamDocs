// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Yaml;

/// <summary>
/// Byte-level helper that pulls the raw scalar / inline-list / block-list bytes
/// of one or more top-level frontmatter keys into an
/// <see cref="IBufferWriter{T}"/> sink. Used by the search plugin to fold
/// caller-selected frontmatter fields (title, summary, tags, author…) into
/// each page's searchable text without materializing a string.
/// </summary>
public static class FrontmatterValueExtractor
{
    /// <summary>Returns true when the top-level block-list under <paramref name="listKey"/> contains an item whose trimmed value equals <paramref name="entry"/>.</summary>
    /// <param name="source">UTF-8 markdown source bytes (frontmatter + body).</param>
    /// <param name="listKey">UTF-8 list-key bytes (e.g. <c>"hide"u8</c>).</param>
    /// <param name="entry">UTF-8 entry bytes to look for (e.g. <c>"navigation"u8</c>).</param>
    /// <returns>True when the entry is present in the block-list.</returns>
    public static bool ListContains(ReadOnlySpan<byte> source, ReadOnlySpan<byte> listKey, ReadOnlySpan<byte> entry) =>
        !listKey.IsEmpty && !entry.IsEmpty && TryGetFrontmatterAndKeyLine(source, listKey, out var frontmatter, out var lineEnd, out _)
        && BlockListContains(frontmatter, lineEnd, entry);

    /// <summary>Returns the trimmed inline scalar value bytes for the top-level key <paramref name="keyBytes"/>, or empty when absent / non-scalar.</summary>
    /// <param name="source">UTF-8 markdown source bytes (frontmatter + body).</param>
    /// <param name="keyBytes">UTF-8 key bytes (e.g. <c>"title"u8</c>).</param>
    /// <returns>Trimmed inline scalar slice into <paramref name="source"/>, or empty when no scalar value follows the key.</returns>
    public static ReadOnlySpan<byte> GetScalar(ReadOnlySpan<byte> source, ReadOnlySpan<byte> keyBytes) =>
        keyBytes.IsEmpty || !TryGetFrontmatterAndKeyLine(source, keyBytes, out _, out _, out var afterColon)
            ? []
            : YamlByteScanner.TrimWhitespace(afterColon);

    /// <summary>
    /// Reads the frontmatter values for every key in <paramref name="keys"/>
    /// from <paramref name="source"/> and appends each (separated by a single
    /// space) to <paramref name="sink"/>.
    /// </summary>
    /// <param name="source">UTF-8 markdown source bytes (frontmatter + body).</param>
    /// <param name="keys">Top-level UTF-8 key bytes to extract — caller-supplied set, typically small. Encode once at builder time so per-page extraction does no encoding work.</param>
    /// <param name="sink">UTF-8 sink the matched values are appended into.</param>
    public static void AppendKeysTo(ReadOnlySpan<byte> source, byte[][] keys, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(sink);
        if (keys.Length is 0 || !YamlByteScanner.TryFindFrontmatter(source, out var closerStart, out _))
        {
            return;
        }

        // closerStart points at the closing `---` line so the block-list /
        // continuation walker doesn't pick it up as another value row.
        var frontmatter = source[..closerStart];
        for (var k = 0; k < keys.Length; k++)
        {
            var keyBytes = keys[k];
            if (keyBytes is null or [])
            {
                continue;
            }

            AppendValueIfPresent(frontmatter, keyBytes, sink);
        }
    }

    /// <summary>Locates the first top-level line whose key equals <paramref name="keyBytes"/>; yields the frontmatter slice, the cursor past the key line, and the bytes after the colon.</summary>
    /// <param name="source">UTF-8 markdown source bytes.</param>
    /// <param name="keyBytes">UTF-8 key bytes.</param>
    /// <param name="frontmatter">Frontmatter slice on success.</param>
    /// <param name="lineEnd">Offset just past the matched key line on success.</param>
    /// <param name="afterColon">Bytes after the colon on the matched line on success.</param>
    /// <returns>True when the key line was found.</returns>
    private static bool TryGetFrontmatterAndKeyLine(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> keyBytes,
        out ReadOnlySpan<byte> frontmatter,
        out int lineEnd,
        out ReadOnlySpan<byte> afterColon)
    {
        afterColon = default;
        lineEnd = 0;
        frontmatter = default;
        if (!YamlByteScanner.TryFindFrontmatter(source, out var closerStart, out _))
        {
            return false;
        }

        frontmatter = source[..closerStart];
        return TryFindTopLevelKey(frontmatter, keyBytes, out lineEnd, out afterColon);
    }

    /// <summary>Walks <paramref name="frontmatter"/> line-by-line looking for a top-level <c>{key}:</c> match.</summary>
    /// <param name="frontmatter">Frontmatter bytes.</param>
    /// <param name="keyBytes">UTF-8 key bytes.</param>
    /// <param name="lineEnd">Offset just past the matched key line on success; otherwise <c>0</c>.</param>
    /// <param name="afterColon">Bytes after the colon on the matched line on success; otherwise empty.</param>
    /// <returns>True on match.</returns>
    private static bool TryFindTopLevelKey(
        ReadOnlySpan<byte> frontmatter,
        ReadOnlySpan<byte> keyBytes,
        out int lineEnd,
        out ReadOnlySpan<byte> afterColon)
    {
        lineEnd = 0;
        afterColon = default;
        var cursor = 0;
        while (cursor < frontmatter.Length)
        {
            var thisLineEnd = YamlByteScanner.LineEnd(frontmatter, cursor);
            var trimmed = YamlByteScanner.TrimLeading(frontmatter[cursor..thisLineEnd]);
            if (trimmed.Length > keyBytes.Length
                && trimmed.StartsWith(keyBytes)
                && trimmed[keyBytes.Length] is (byte)':')
            {
                lineEnd = thisLineEnd;
                afterColon = trimmed[(keyBytes.Length + 1)..];
                return true;
            }

            cursor = thisLineEnd;
        }

        return false;
    }

    /// <summary>Appends the value bytes of the top-level <paramref name="keyBytes"/> in <paramref name="frontmatter"/> to <paramref name="sink"/> when present.</summary>
    /// <remarks>The appended value is preceded by a single space-byte delimiter.</remarks>
    /// <param name="frontmatter">Frontmatter bytes.</param>
    /// <param name="keyBytes">UTF-8 key bytes.</param>
    /// <param name="sink">Output sink.</param>
    private static void AppendValueIfPresent(ReadOnlySpan<byte> frontmatter, ReadOnlySpan<byte> keyBytes, IBufferWriter<byte> sink)
    {
        if (!TryFindTopLevelKey(frontmatter, keyBytes, out var lineEnd, out var afterColon))
        {
            return;
        }

        AppendInlineValue(afterColon, sink);
        AppendBlockChildren(frontmatter, lineEnd, sink);
    }

    /// <summary>Appends the inline portion of a value (everything after the colon, on the same line) to <paramref name="sink"/> with a leading space.</summary>
    /// <param name="span">Bytes after the colon, including any trailing newline.</param>
    /// <param name="sink">Output sink.</param>
    private static void AppendInlineValue(ReadOnlySpan<byte> span, IBufferWriter<byte> sink)
    {
        var trimmed = YamlByteScanner.TrimWhitespace(span);
        if (trimmed.IsEmpty)
        {
            return;
        }

        WriteWithSpace(sink, trimmed);
    }

    /// <summary>Reads one indented continuation / block-list child line; returns false when the next line is no longer a child.</summary>
    /// <param name="frontmatter">Frontmatter bytes.</param>
    /// <param name="cursor">Cursor positioned at the candidate child line; advanced past the consumed line on success.</param>
    /// <param name="value">Trimmed child value on success; empty otherwise.</param>
    /// <returns>True when a child line was consumed; false at end-of-block.</returns>
    private static bool TryReadBlockChild(ReadOnlySpan<byte> frontmatter, ref int cursor, out ReadOnlySpan<byte> value)
    {
        value = default;
        if (cursor >= frontmatter.Length)
        {
            return false;
        }

        var lineEnd = YamlByteScanner.LineEnd(frontmatter, cursor);
        var line = frontmatter[cursor..lineEnd];
        if (line.IsEmpty || line[0] is not ((byte)' ' or (byte)'\t' or (byte)'-'))
        {
            return false;
        }

        // Strip leading whitespace and an optional `-` list marker.
        var trimmed = YamlByteScanner.TrimLeading(line);
        if (trimmed is [(byte)'-', ..])
        {
            trimmed = YamlByteScanner.TrimLeading(trimmed[1..]);
        }

        value = YamlByteScanner.TrimWhitespace(trimmed);
        cursor = lineEnd;
        return true;
    }

    /// <summary>Appends every indented continuation / block-list line that follows <paramref name="cursor"/> to <paramref name="sink"/> (each preceded by a single space).</summary>
    /// <param name="frontmatter">Frontmatter bytes.</param>
    /// <param name="cursor">Cursor positioned at the first child line.</param>
    /// <param name="sink">Output sink.</param>
    private static void AppendBlockChildren(ReadOnlySpan<byte> frontmatter, int cursor, IBufferWriter<byte> sink)
    {
        while (TryReadBlockChild(frontmatter, ref cursor, out var value))
        {
            if (!value.IsEmpty)
            {
                WriteWithSpace(sink, value);
            }
        }
    }

    /// <summary>Probes the indented block-list children that follow <paramref name="cursor"/> for an item that equals <paramref name="entry"/>.</summary>
    /// <param name="frontmatter">Frontmatter bytes.</param>
    /// <param name="cursor">Cursor positioned at the first child line.</param>
    /// <param name="entry">Entry bytes to match.</param>
    /// <returns>True on match.</returns>
    private static bool BlockListContains(ReadOnlySpan<byte> frontmatter, int cursor, ReadOnlySpan<byte> entry)
    {
        while (TryReadBlockChild(frontmatter, ref cursor, out var value))
        {
            if (value.SequenceEqual(entry))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes a leading space byte then <paramref name="value"/> into <paramref name="sink"/> in one allocation.</summary>
    /// <param name="sink">Output sink.</param>
    /// <param name="value">Value bytes to append (must be non-empty).</param>
    private static void WriteWithSpace(IBufferWriter<byte> sink, ReadOnlySpan<byte> value)
    {
        var dst = sink.GetSpan(value.Length + 1);
        dst[0] = (byte)' ';
        value.CopyTo(dst[1..]);
        sink.Advance(value.Length + 1);
    }
}
