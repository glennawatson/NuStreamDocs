// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Layouts;

/// <summary>Frontmatter helpers used by the layouts plugin to populate the <c>page.*</c> variable bag.</summary>
internal static class FrontmatterReader
{
    /// <summary>Returns the trimmed inline scalar value bytes for a top-level frontmatter key.</summary>
    /// <param name="source">UTF-8 markdown bytes (frontmatter + body).</param>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <returns>Trimmed scalar slice into <paramref name="source"/>, or empty when the key is missing or has no inline scalar.</returns>
    public static ReadOnlySpan<byte> GetScalar(ReadOnlySpan<byte> source, ReadOnlySpan<byte> key) =>
        Unquote(FrontmatterValueExtractor.GetScalar(source, key));

    /// <summary>Walks every top-level scalar frontmatter key and adds it to <paramref name="values"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="values">Sink dictionary keyed by UTF-8 key bytes.</param>
    public static void AppendScalars(ReadOnlySpan<byte> source, Dictionary<byte[], byte[]> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!YamlByteScanner.TryFindFrontmatter(source, out var closerStart, out _))
        {
            return;
        }

        var frontmatter = source[..closerStart];
        var cursor = 0;
        while (cursor < frontmatter.Length)
        {
            var lineEnd = Utf8LineSpan.LfLineEnd(frontmatter, cursor);
            ConsumeLine(frontmatter[cursor..lineEnd], values);
            cursor = lineEnd;
        }
    }

    /// <summary>Adds a single frontmatter <paramref name="line"/>'s key/value pair to <paramref name="values"/> when it qualifies as a simple top-level scalar.</summary>
    /// <param name="line">Single frontmatter line.</param>
    /// <param name="values">Sink dictionary.</param>
    private static void ConsumeLine(ReadOnlySpan<byte> line, Dictionary<byte[], byte[]> values)
    {
        if (!YamlByteScanner.IsTopLevelKey(line))
        {
            return;
        }

        var key = YamlByteScanner.KeyOf(line);
        if (key.IsEmpty || !IsSimpleKey(key))
        {
            return;
        }

        var afterColon = line[(line.IndexOf((byte)':') + 1)..];
        var scalar = Unquote(YamlByteScanner.TrimWhitespace(afterColon));
        if (scalar.IsEmpty)
        {
            return;
        }

        values[key.ToArray()] = scalar.ToArray();
    }

    /// <summary>Strips matching <c>"…"</c> / <c>'…'</c> quote pairs from <paramref name="span"/>.</summary>
    /// <param name="span">Source bytes.</param>
    /// <returns>Inner bytes when quoted; passthrough otherwise.</returns>
    private static ReadOnlySpan<byte> Unquote(ReadOnlySpan<byte> span) =>
        span is [(byte)'"', .., (byte)'"'] or [(byte)'\'', .., (byte)'\'']
            ? span[1..^1]
            : span;

    /// <summary>True when every byte of <paramref name="key"/> is a letter, digit, dot, underscore, or hyphen.</summary>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <returns>True for keys safe to expose as <c>{{ page.X }}</c> names.</returns>
    private static bool IsSimpleKey(ReadOnlySpan<byte> key)
    {
        for (var i = 0; i < key.Length; i++)
        {
            if (!IsKeyByte(key[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True for ASCII bytes that may appear inside a simple frontmatter key.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for letters, digits, dot, underscore, or hyphen.</returns>
    private static bool IsKeyByte(byte b) =>
        b is >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or >= (byte)'0' and <= (byte)'9'
          or (byte)'.'
          or (byte)'_'
          or (byte)'-';
}
