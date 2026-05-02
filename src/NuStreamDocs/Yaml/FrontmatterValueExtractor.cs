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

    /// <summary>Appends the value bytes of the top-level <paramref name="keyBytes"/> in <paramref name="frontmatter"/> to <paramref name="sink"/> when present.</summary>
    /// <remarks>The appended value is preceded by a single space-byte delimiter.</remarks>
    /// <param name="frontmatter">Frontmatter bytes.</param>
    /// <param name="keyBytes">UTF-8 key bytes.</param>
    /// <param name="sink">Output sink.</param>
    private static void AppendValueIfPresent(ReadOnlySpan<byte> frontmatter, ReadOnlySpan<byte> keyBytes, IBufferWriter<byte> sink)
    {
        var cursor = 0;
        while (cursor < frontmatter.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(frontmatter, cursor);
            var line = frontmatter[cursor..lineEnd];
            var trimmed = YamlByteScanner.TrimLeading(line);
            if (trimmed.Length > keyBytes.Length
                && trimmed.StartsWith(keyBytes)
                && trimmed[keyBytes.Length] is (byte)':')
            {
                AppendInlineValue(trimmed[(keyBytes.Length + 1)..], sink);
                AppendBlockChildren(frontmatter, lineEnd, sink);
                return;
            }

            cursor = lineEnd;
        }
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

        var dst = sink.GetSpan(trimmed.Length + 1);
        dst[0] = (byte)' ';
        trimmed.CopyTo(dst[1..]);
        sink.Advance(trimmed.Length + 1);
    }

    /// <summary>Appends every indented continuation / block-list line that follows <paramref name="cursor"/> to <paramref name="sink"/> (each preceded by a single space).</summary>
    /// <param name="frontmatter">Frontmatter bytes.</param>
    /// <param name="cursor">Cursor positioned at the line just after the key line.</param>
    /// <param name="sink">Output sink.</param>
    private static void AppendBlockChildren(ReadOnlySpan<byte> frontmatter, int cursor, IBufferWriter<byte> sink)
    {
        while (cursor < frontmatter.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(frontmatter, cursor);
            var line = frontmatter[cursor..lineEnd];
            if (line.IsEmpty)
            {
                break;
            }

            var first = line[0];
            if (first is not ((byte)' ' or (byte)'\t' or (byte)'-'))
            {
                break;
            }

            // Strip the leading whitespace and optional `- ` list marker.
            var trimmed = YamlByteScanner.TrimLeading(line);
            if (trimmed is [(byte)'-', ..])
            {
                trimmed = YamlByteScanner.TrimLeading(trimmed[1..]);
            }

            var withoutNewline = YamlByteScanner.TrimWhitespace(trimmed);
            if (!withoutNewline.IsEmpty)
            {
                var dst = sink.GetSpan(withoutNewline.Length + 1);
                dst[0] = (byte)' ';
                withoutNewline.CopyTo(dst[1..]);
                sink.Advance(withoutNewline.Length + 1);
            }

            cursor = lineEnd;
        }
    }
}
