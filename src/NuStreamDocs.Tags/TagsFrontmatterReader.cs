// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tags;

/// <summary>
/// Reads the <c>tags:</c> field from a page's YAML frontmatter.
/// Recognizes both inline (<c>tags: [a, b]</c>) and block-list
/// (<c>tags:\n  - a\n  - b</c>) shapes; returns an empty array
/// when no frontmatter or no tag field is present.
/// </summary>
internal static class TagsFrontmatterReader
{
    /// <summary>Gets the UTF-8 bytes of the <c>tags:</c> key prefix.</summary>
    private static ReadOnlySpan<byte> TagsKey => "tags:"u8;

    /// <summary>Reads tags from <paramref name="source"/>'s frontmatter; returns empty when no frontmatter or no tags field.</summary>
    /// <param name="source">UTF-8 markdown source bytes.</param>
    /// <returns>A possibly empty array of UTF-8 tag byte arrays.</returns>
    public static byte[][] Read(ReadOnlySpan<byte> source)
    {
        if (!YamlByteScanner.TryFindFrontmatter(source, out _, out var bodyStart))
        {
            return [];
        }

        var frontmatter = source[..bodyStart];
        var tagsLineStart = FindTagsLine(frontmatter);
        return tagsLineStart < 0 ? [] : ParseTagsValue(frontmatter, tagsLineStart);
    }

    /// <summary>Finds the offset of a top-level <c>tags:</c> key in the frontmatter span.</summary>
    /// <param name="frontmatter">UTF-8 frontmatter bytes (between and including the delimiters).</param>
    /// <returns>Offset of the byte immediately after the colon, or -1 when absent.</returns>
    private static int FindTagsLine(ReadOnlySpan<byte> frontmatter)
    {
        var cursor = 0;
        while (cursor < frontmatter.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(frontmatter, cursor);
            var line = frontmatter[cursor..lineEnd];
            var trimmed = YamlByteScanner.TrimLeading(line);
            var indent = line.Length - trimmed.Length;
            if (indent is 0 && trimmed.StartsWith(TagsKey))
            {
                return cursor + indent + TagsKey.Length;
            }

            cursor = lineEnd;
        }

        return -1;
    }

    /// <summary>Decodes a YAML value attached to the <c>tags:</c> key (inline list or block list).</summary>
    /// <param name="frontmatter">UTF-8 frontmatter bytes.</param>
    /// <param name="valueStart">Offset just past the colon.</param>
    /// <returns>Parsed UTF-8 tag arrays, or empty when the value is malformed.</returns>
    private static byte[][] ParseTagsValue(ReadOnlySpan<byte> frontmatter, int valueStart)
    {
        var lineEnd = YamlByteScanner.LineEnd(frontmatter, valueStart);
        var inline = YamlByteScanner.TrimWhitespace(frontmatter[valueStart..lineEnd]);
        if (inline is [(byte)'[', .., (byte)']'])
        {
            return ParseInlineList(inline[1..^1]);
        }

        if (!inline.IsEmpty)
        {
            // Single inline scalar (`tags: foo` or `tags: "foo, bar"`).
            return ParseInlineList(inline);
        }

        return ParseBlockList(frontmatter, lineEnd);
    }

    /// <summary>Splits a comma-separated inline list (with optional surrounding quotes per token).</summary>
    /// <param name="span">Bytes between the brackets, or the bare scalar.</param>
    /// <returns>UTF-8 tag arrays.</returns>
    private static byte[][] ParseInlineList(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
        {
            return [];
        }

        List<byte[]> tags = new(4);
        var cursor = 0;
        while (cursor < span.Length)
        {
            var commaRel = span[cursor..].IndexOf((byte)',');
            var tokenEnd = commaRel < 0 ? span.Length : cursor + commaRel;
            AppendTag(tags, span[cursor..tokenEnd]);
            if (commaRel < 0)
            {
                break;
            }

            cursor = tokenEnd + 1;
        }

        return [.. tags];
    }

    /// <summary>Reads a YAML block list (<c>- foo</c> per line) starting at <paramref name="cursor"/> until indentation drops or a non-block line is hit.</summary>
    /// <param name="frontmatter">UTF-8 frontmatter bytes.</param>
    /// <param name="cursor">Offset to start scanning from.</param>
    /// <returns>UTF-8 tag arrays.</returns>
    private static byte[][] ParseBlockList(ReadOnlySpan<byte> frontmatter, int cursor)
    {
        List<byte[]> tags = new(4);
        while (cursor < frontmatter.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(frontmatter, cursor);
            var line = frontmatter[cursor..lineEnd];
            var trimmed = YamlByteScanner.TrimLeading(line);
            if (trimmed.IsEmpty)
            {
                cursor = lineEnd;
                continue;
            }

            if (trimmed[0] is not (byte)'-' || trimmed.StartsWith(YamlByteScanner.FrontmatterDelimiter))
            {
                break;
            }

            AppendTag(tags, YamlByteScanner.TrimLeading(trimmed[1..]));
            cursor = lineEnd;
        }

        return [.. tags];
    }

    /// <summary>Trims YAML quote markers and whitespace from <paramref name="span"/> and adds the result to <paramref name="tags"/> when non-empty.</summary>
    /// <param name="tags">Destination list.</param>
    /// <param name="span">Token bytes.</param>
    private static void AppendTag(List<byte[]> tags, ReadOnlySpan<byte> span)
    {
        var trimmed = YamlByteScanner.TrimWhitespace(span);
        var unquoted = YamlByteScanner.Unquote(trimmed);
        if (unquoted.IsEmpty)
        {
            return;
        }

        tags.Add(unquoted.ToArray());
    }
}
