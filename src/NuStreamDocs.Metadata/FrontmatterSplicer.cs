// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Metadata;

/// <summary>
/// Splices <see cref="MetadataRegistry"/> keys into a page's UTF-8
/// source bytes. Per-page metadata wins over inherited keys, so we
/// only ever *append* keys not already declared by the page itself.
/// </summary>
internal static class FrontmatterSplicer
{
    /// <summary>UTF-8 bytes of the YAML frontmatter delimiter <c>---\n</c>.</summary>
    private static readonly byte[] DelimiterLine = [.. "---\n"u8];

    /// <summary>Writes <paramref name="source"/> into <paramref name="writer"/>, splicing any inherited keys from <paramref name="extra"/> that the page hasn't already defined.</summary>
    /// <param name="source">UTF-8 page bytes (frontmatter + body, or just body).</param>
    /// <param name="extra">Merged inherited-keys body (no surrounding <c>---</c>); empty for a no-op pass-through.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Splice(ReadOnlySpan<byte> source, ReadOnlySpan<byte> extra, IBufferWriter<byte> writer)
    {
        if (extra.IsEmpty)
        {
            Write(writer, source);
            return;
        }

        var bodyStart = FindFrontmatterEnd(source, out var hasFrontmatter);
        if (!hasFrontmatter)
        {
            // No existing frontmatter — wrap the inherited block.
            Write(writer, DelimiterLine);
            Write(writer, extra);
            EnsureTrailingNewline(writer, extra);
            Write(writer, DelimiterLine);
            Write(writer, source);
            return;
        }

        // Existing frontmatter: copy it through up to the closing `---` line,
        // append any inherited keys the page didn't declare, then close.
        var closingLineStart = FindClosingDelimiterLine(source, bodyStart);
        var existing = source[..closingLineStart];
        Write(writer, existing);

        AppendFreshKeys(existing, extra, writer);
        Write(writer, source[closingLineStart..]);
    }

    /// <summary>Locates the end of the frontmatter block (the offset of the byte just past the closing <c>---</c> line).</summary>
    /// <param name="source">UTF-8 page bytes.</param>
    /// <param name="hasFrontmatter">Set to <see langword="true"/> when a complete <c>---</c>...<c>---</c> block was found.</param>
    /// <returns>Byte offset of the first body byte; 0 when no frontmatter is present.</returns>
    private static int FindFrontmatterEnd(ReadOnlySpan<byte> source, out bool hasFrontmatter)
    {
        hasFrontmatter = false;
        if (!source.StartsWith("---"u8))
        {
            return 0;
        }

        var afterFirst = "---"u8.Length;
        if (afterFirst >= source.Length || source[afterFirst] is not (byte)'\n' and not (byte)'\r')
        {
            return 0;
        }

        var cursor = YamlByteScanner.LineEnd(source, 0);
        while (cursor < source.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(source, cursor);
            var line = source[cursor..lineEnd].TrimEnd((byte)'\n').TrimEnd((byte)'\r');
            if (line.SequenceEqual("---"u8))
            {
                hasFrontmatter = true;
                return lineEnd;
            }

            cursor = lineEnd;
        }

        return 0;
    }

    /// <summary>Returns the offset of the first byte of the closing <c>---</c> line, given the offset of the first body byte (<paramref name="bodyStart"/>).</summary>
    /// <param name="source">UTF-8 page bytes.</param>
    /// <param name="bodyStart">Byte just past the closing <c>---</c> line.</param>
    /// <returns>Offset of the closing <c>---</c> line.</returns>
    private static int FindClosingDelimiterLine(ReadOnlySpan<byte> source, int bodyStart)
    {
        // Walk backwards from bodyStart to the start of the previous line.
        var cursor = bodyStart - 1;
        if (cursor >= 0 && source[cursor] is (byte)'\n')
        {
            cursor--;
        }

        while (cursor >= 0 && source[cursor] is not (byte)'\n')
        {
            cursor--;
        }

        return cursor + 1;
    }

    /// <summary>Appends every top-level key from <paramref name="extra"/> to <paramref name="writer"/> that <paramref name="existingFrontmatter"/> hasn't already defined.</summary>
    /// <param name="existingFrontmatter">Page's own frontmatter bytes (including opening <c>---</c>).</param>
    /// <param name="extra">Inherited-keys body.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void AppendFreshKeys(ReadOnlySpan<byte> existingFrontmatter, ReadOnlySpan<byte> extra, IBufferWriter<byte> writer)
    {
        var existingKeys = CollectKeys(existingFrontmatter);
        try
        {
            var cursor = 0;
            while (cursor < extra.Length)
            {
                var lineStart = cursor;
                var lineEnd = YamlByteScanner.LineEnd(extra, cursor);
                var line = extra[lineStart..lineEnd];
                if (!YamlByteScanner.IsTopLevelKey(line))
                {
                    cursor = lineEnd;
                    continue;
                }

                var key = YamlByteScanner.KeyOf(line);
                if (existingKeys.ContainsByUtf8(key))
                {
                    cursor = YamlByteScanner.AdvancePastValue(extra, lineEnd);
                    continue;
                }

                var valueEnd = YamlByteScanner.AdvancePastValue(extra, lineEnd);
                Write(writer, extra[lineStart..valueEnd]);
                if (valueEnd > 0 && extra[valueEnd - 1] is not (byte)'\n')
                {
                    Write(writer, "\n"u8);
                }

                cursor = valueEnd;
            }
        }
        finally
        {
            existingKeys.Clear();
        }
    }

    /// <summary>Collects every top-level key from <paramref name="frontmatter"/> into a fresh byte-keyed hash set.</summary>
    /// <param name="frontmatter">Page frontmatter bytes (including opening <c>---</c>).</param>
    /// <returns>Top-level UTF-8 key bytes, ordinal-compared via <see cref="ByteArrayComparer.Instance"/>.</returns>
    private static HashSet<byte[]> CollectKeys(ReadOnlySpan<byte> frontmatter)
    {
        HashSet<byte[]> keys = new(ByteArrayComparer.Instance);
        var cursor = YamlByteScanner.LineEnd(frontmatter, 0);
        while (cursor < frontmatter.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(frontmatter, cursor);
            var line = frontmatter[cursor..lineEnd];
            if (YamlByteScanner.IsTopLevelKey(line))
            {
                var key = YamlByteScanner.KeyOf(line);
                keys.Add(key.ToArray());
            }

            cursor = lineEnd;
        }

        return keys;
    }

    /// <summary>Bulk-writes <paramref name="bytes"/> into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes.</param>
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

    /// <summary>Writes a single newline when <paramref name="extra"/> doesn't end with one.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="extra">Bytes just written before this call.</param>
    private static void EnsureTrailingNewline(IBufferWriter<byte> writer, ReadOnlySpan<byte> extra)
    {
        if (!extra.IsEmpty && extra[^1] is (byte)'\n')
        {
            return;
        }

        Write(writer, "\n"u8);
    }
}
