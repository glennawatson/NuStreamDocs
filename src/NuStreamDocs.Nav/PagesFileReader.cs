// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Yaml;

namespace NuStreamDocs.Nav;

/// <summary>
/// Reads a <c>.pages</c> YAML override (literate-nav). Supports the
/// minimal subset:
/// <code>
/// title: Custom Title
/// hide: true
/// nav:
///   - intro.md
///   - subsection
///   - reference.md
/// </code>
/// </summary>
internal static class PagesFileReader
{
    /// <summary>Reads a <c>.pages</c> file from <paramref name="path"/>; returns <see cref="PagesFile.Empty"/> when missing.</summary>
    /// <param name="path">Absolute path to the candidate <c>.pages</c> file.</param>
    /// <returns>Parsed override.</returns>
    public static PagesFile ReadOrEmpty(string path) => !File.Exists(path) ? PagesFile.Empty : Parse(File.ReadAllBytes(path));

    /// <summary>Parses <paramref name="source"/> into a <see cref="PagesFile"/>.</summary>
    /// <param name="source">UTF-8 file bytes.</param>
    /// <returns>Parsed override; defaults preserved when keys are absent.</returns>
    public static PagesFile Parse(ReadOnlySpan<byte> source)
    {
        byte[] title = [];
        var hide = false;
        byte[][] nav = [];

        var cursor = 0;
        while (cursor < source.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(source, cursor);
            var line = source[cursor..lineEnd];
            var trimmed = YamlByteScanner.TrimLeading(line);
            var indent = line.Length - trimmed.Length;

            if (indent is 0 && !trimmed.IsEmpty)
            {
                if (trimmed.StartsWith("title:"u8))
                {
                    title = ReadScalar(trimmed["title:"u8.Length..]);
                }
                else if (trimmed.StartsWith("hide:"u8))
                {
                    hide = ReadBool(trimmed["hide:"u8.Length..]);
                }
                else if (trimmed.StartsWith("nav:"u8))
                {
                    nav = ReadBlockList(source, lineEnd);
                }
            }

            cursor = lineEnd;
        }

        return new(title, nav, hide);
    }

    /// <summary>Reads a YAML block list starting at <paramref name="cursor"/> until indentation drops or a non-list line is hit.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Offset to start scanning from (just past <c>nav:</c>).</param>
    /// <returns>Entry bytes.</returns>
    private static byte[][] ReadBlockList(ReadOnlySpan<byte> source, int cursor)
    {
        List<byte[]> entries = new(8);
        while (cursor < source.Length)
        {
            var lineEnd = YamlByteScanner.LineEnd(source, cursor);
            var line = source[cursor..lineEnd];
            var trimmed = YamlByteScanner.TrimLeading(line);
            if (trimmed.IsEmpty)
            {
                cursor = lineEnd;
                continue;
            }

            if (trimmed[0] is not (byte)'-')
            {
                break;
            }

            var entry = ReadScalar(YamlByteScanner.TrimLeading(trimmed[1..]));
            if (entry.Length > 0)
            {
                entries.Add(entry);
            }

            cursor = lineEnd;
        }

        return [.. entries];
    }

    /// <summary>Reads a YAML scalar — strips optional quotes and a trailing comment.</summary>
    /// <param name="span">Bytes after the key.</param>
    /// <returns>Decoded bytes; empty when the scalar is absent.</returns>
    private static byte[] ReadScalar(ReadOnlySpan<byte> span)
    {
        var trimmed = YamlByteScanner.TrimWhitespace(span);
        var unquoted = YamlByteScanner.Unquote(trimmed);
        return unquoted.IsEmpty ? [] : unquoted.ToArray();
    }

    /// <summary>Reads a YAML boolean (true / false / yes / no).</summary>
    /// <param name="span">Bytes after the key.</param>
    /// <returns>True for <c>true</c>/<c>yes</c>; false otherwise.</returns>
    private static bool ReadBool(ReadOnlySpan<byte> span)
    {
        var trimmed = YamlByteScanner.TrimWhitespace(span);
        return trimmed.SequenceEqual("true"u8) || trimmed.SequenceEqual("yes"u8);
    }
}
