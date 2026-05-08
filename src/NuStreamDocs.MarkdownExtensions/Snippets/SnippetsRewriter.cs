// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.MarkdownExtensions.Snippets;

/// <summary>
/// Stateless UTF-8 snippets rewriter. Replaces lines of the form
/// <c>--8&lt;-- "path/to/file.md"</c> with the contents of the referenced file,
/// resolved against an ordered list of base directories.
/// </summary>
internal static class SnippetsRewriter
{
    /// <summary>Maximum nested-include depth before the rewriter stops recursing.</summary>
    private const int MaxIncludeDepth = 10;

    /// <summary>Length of a <c>\r\n</c> line terminator in bytes.</summary>
    private const int CrLfLength = 2;

    /// <summary>Gets the marker that introduces a snippet inclusion line.</summary>
    private static ReadOnlySpan<byte> Marker => "--8<--"u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>, expanding every snippet directive.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="basePaths">Ordered list of directories to resolve include paths against; the first hit wins.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, DirectoryPath[] basePaths)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(basePaths);
        Expand(source, writer, basePaths, depth: 0);
    }

    /// <summary>Expands <paramref name="source"/> recursively, capped at <see cref="MaxIncludeDepth"/>.</summary>
    /// <param name="source">UTF-8 input.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="basePaths">Resolution roots.</param>
    /// <param name="depth">Current recursion depth.</param>
    private static void Expand(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, DirectoryPath[] basePaths, int depth)
    {
        var cursor = 0;
        while (cursor < source.Length)
        {
            var lineEnd = FindLineEnd(source, cursor);
            var lineBody = source[cursor..lineEnd];
            if (TryParseInclude(lineBody, out var path) && depth < MaxIncludeDepth && TryReadSnippet(path, basePaths, out var included))
            {
                Expand(included, writer, basePaths, depth + 1);
                cursor = AdvancePastLineTerminator(source, lineEnd);
                continue;
            }

            // Pass the line through verbatim, including its line terminator.
            var nextCursor = AdvancePastLineTerminator(source, lineEnd);
            writer.Write(source[cursor..nextCursor]);
            cursor = nextCursor;
        }
    }

    /// <summary>Returns true when <paramref name="line"/> matches the snippet directive shape.</summary>
    /// <param name="line">Single source line (no terminator).</param>
    /// <param name="path">Captured path bytes on success.</param>
    /// <returns>True when the line is a snippet directive.</returns>
    private static bool TryParseInclude(ReadOnlySpan<byte> line, out ReadOnlySpan<byte> path)
    {
        path = default;
        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(line);
        if (!trimmed.StartsWith(Marker))
        {
            return false;
        }

        var rest = AsciiByteHelpers.TrimAsciiWhitespace(trimmed[Marker.Length..]);
        if (rest.Length < 2 || rest[0] is not (byte)'"' || rest[^1] is not (byte)'"')
        {
            return false;
        }

        path = rest[1..^1];
        return path.Length > 0;
    }

    /// <summary>Resolves <paramref name="path"/> against each base directory in order and reads the first match.</summary>
    /// <param name="path">UTF-8 bytes of the snippet path (relative).</param>
    /// <param name="basePaths">Resolution roots.</param>
    /// <param name="contents">File bytes on success.</param>
    /// <returns>True when one of the base directories yielded a readable file.</returns>
    private static bool TryReadSnippet(ReadOnlySpan<byte> path, DirectoryPath[] basePaths, out ReadOnlySpan<byte> contents)
    {
        contents = default;
        var pathString = System.Text.Encoding.UTF8.GetString(path);
        for (var i = 0; i < basePaths.Length; i++)
        {
            var candidate = Path.GetFullPath(pathString, basePaths[i].Value);
            if (!File.Exists(candidate))
            {
                continue;
            }

            contents = File.ReadAllBytes(candidate);
            return true;
        }

        return false;
    }

    /// <summary>Returns the index of the first line-terminator byte at or after <paramref name="cursor"/>, or <paramref name="source"/>.Length when none.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="cursor">Search-start offset.</param>
    /// <returns>Exclusive end of the current line content (before any <c>\r</c> or <c>\n</c>).</returns>
    private static int FindLineEnd(ReadOnlySpan<byte> source, int cursor)
    {
        var rest = source[cursor..];
        var hit = rest.IndexOfAny((byte)'\r', (byte)'\n');
        return hit < 0 ? source.Length : cursor + hit;
    }

    /// <summary>Advances past the line terminator at <paramref name="lineEnd"/>; consumes the matched <c>\r\n</c>, <c>\n</c>, or <c>\r</c>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="lineEnd">Index of the first line-terminator byte (or source length).</param>
    /// <returns>Index of the next line's first byte (or source length).</returns>
    private static int AdvancePastLineTerminator(ReadOnlySpan<byte> source, int lineEnd)
    {
        if (lineEnd >= source.Length)
        {
            return source.Length;
        }

        if (source[lineEnd] is (byte)'\r' && lineEnd + 1 < source.Length && source[lineEnd + 1] is (byte)'\n')
        {
            return lineEnd + CrLfLength;
        }

        return lineEnd + 1;
    }
}
