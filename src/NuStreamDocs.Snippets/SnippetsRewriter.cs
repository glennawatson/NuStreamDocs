// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Snippets;

/// <summary>
/// Stateless UTF-8 snippet-include rewriter. Walks the source
/// byte stream, replacing <c>--8&lt;-- "file"</c> include markers
/// at line starts with the contents of the referenced file
/// resolved against a base directory. Recursion is bounded by
/// <see cref="MaxIncludeDepth"/> so a self-referencing snippet
/// renders an error rather than blowing the stack.
/// </summary>
internal static class SnippetsRewriter
{
    /// <summary>Maximum include nesting depth before a cycle-guard fires.</summary>
    private const int MaxIncludeDepth = 8;

    /// <summary>Gets the marker bytes that introduce an include directive.</summary>
    private static ReadOnlySpan<byte> IncludeMarker => "--8<--"u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>, splicing every <c>--8&lt;-- "file"</c> include inline.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="baseDirectory">Absolute path to resolve include targets against.</param>
    /// <param name="fileCache">Byte-keyed snippet cache scoped to the current build.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, DirectoryPath baseDirectory, Dictionary<byte[], byte[]> fileCache, IBufferWriter<byte> writer)
    {
        HashSet<byte[]> visited = new(ByteArrayComparer.Instance);
        RewriteCore(source, baseDirectory, fileCache, writer, visited, 0);
    }

    /// <summary>Recursive include implementation guarded by <paramref name="depth"/> and <paramref name="visited"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="baseDirectory">Snippet root.</param>
    /// <param name="fileCache">Byte-keyed snippet cache.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="visited">Already-included files (cycle guard, keyed on the include path bytes).</param>
    /// <param name="depth">Current recursion depth.</param>
    private static void RewriteCore(ReadOnlySpan<byte> source, DirectoryPath baseDirectory, Dictionary<byte[], byte[]> fileCache, IBufferWriter<byte> writer, HashSet<byte[]> visited, int depth)
    {
        var i = 0;
        while (i < source.Length)
        {
            var lineStart = MarkdownCodeScanner.AtLineStart(source, i) ? i : -1;
            if (lineStart < 0)
            {
                CopyByte(writer, source[i]);
                i++;
                continue;
            }

            var lineEnd = MarkdownCodeScanner.LineEnd(source, lineStart);
            if (TryParseIncludeLine(source[lineStart..lineEnd], out var pathStart, out var pathLength, out var sectionStart, out var sectionLength))
            {
                var pathBytes = source.Slice(lineStart + pathStart, pathLength);
                var sectionBytes = sectionLength is 0 ? default : source.Slice(lineStart + sectionStart, sectionLength);
                EmitInclude(pathBytes, sectionBytes, baseDirectory, fileCache, writer, visited, depth);
                i = lineEnd;
                continue;
            }

            writer.Write(source[lineStart..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Tries to parse a <c>--8&lt;-- "file"</c> or <c>--8&lt;-- "file#section"</c> directive line.</summary>
    /// <param name="line">UTF-8 bytes of the candidate line.</param>
    /// <param name="pathStart">Offset of the path's first byte within <paramref name="line"/>.</param>
    /// <param name="pathLength">Path byte length.</param>
    /// <param name="sectionStart">Offset of the section name's first byte within <paramref name="line"/> (0 when no section).</param>
    /// <param name="sectionLength">Section name byte length (0 when no section).</param>
    /// <returns>True when the line is a directive.</returns>
    private static bool TryParseIncludeLine(ReadOnlySpan<byte> line, out int pathStart, out int pathLength, out int sectionStart, out int sectionLength)
    {
        pathStart = 0;
        pathLength = 0;
        sectionStart = 0;
        sectionLength = 0;
        var leading = LeadingWhitespaceLength(line);
        var trimmed = line[leading..];
        if (!trimmed.StartsWith(IncludeMarker))
        {
            return false;
        }

        var afterMarker = trimmed[IncludeMarker.Length..];
        var afterMarkerLeading = LeadingWhitespaceLength(afterMarker);
        afterMarker = afterMarker[afterMarkerLeading..];
        if (afterMarker.Length < 2 || afterMarker[0] is not (byte)'"')
        {
            return false;
        }

        var closeQuote = afterMarker[1..].IndexOf((byte)'"');
        if (closeQuote < 0)
        {
            return false;
        }

        // Absolute offset of the opening quote within `line`.
        var quoteOffset = leading + IncludeMarker.Length + afterMarkerLeading;
        var specStart = quoteOffset + 1;
        var pathSpec = line.Slice(specStart, closeQuote);
        var hashIdx = pathSpec.IndexOf((byte)'#');
        pathStart = specStart;
        if (hashIdx >= 0)
        {
            pathLength = hashIdx;
            sectionStart = specStart + hashIdx + 1;
            sectionLength = closeQuote - hashIdx - 1;
        }
        else
        {
            pathLength = closeQuote;
        }

        return pathLength > 0;
    }

    /// <summary>Reads the snippet at <paramref name="pathBytes"/> via byte-keyed cache lookup (resolving + reading on miss) and recursively expands its includes.</summary>
    /// <param name="pathBytes">UTF-8 path bytes lifted from the source span.</param>
    /// <param name="sectionBytes">UTF-8 section-name bytes; empty span means whole-file include.</param>
    /// <param name="baseDirectory">Snippet root.</param>
    /// <param name="fileCache">Build-scoped byte-keyed cache.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="visited">Already-included path bytes (cycle guard).</param>
    /// <param name="depth">Current recursion depth.</param>
    private static void EmitInclude(
        ReadOnlySpan<byte> pathBytes,
        ReadOnlySpan<byte> sectionBytes,
        DirectoryPath baseDirectory,
        Dictionary<byte[], byte[]> fileCache,
        IBufferWriter<byte> writer,
        HashSet<byte[]> visited,
        int depth)
    {
        if (depth >= MaxIncludeDepth)
        {
            EmitError(writer, "snippet include depth exceeded"u8, pathBytes);
            return;
        }

        if (visited.ContainsByUtf8(pathBytes))
        {
            EmitError(writer, "snippet cycle detected"u8, pathBytes);
            return;
        }

        if (!TryGetSnippetBytes(pathBytes, baseDirectory, fileCache, writer, out var bytes))
        {
            return;
        }

        var pathKey = pathBytes.ToArray();
        visited.Add(pathKey);
        try
        {
            if (sectionBytes.IsEmpty)
            {
                RewriteCore(bytes, baseDirectory, fileCache, writer, visited, depth + 1);
                return;
            }

            if (!SnippetSectionExtractor.TryFind(bytes, sectionBytes, out var sectionStart, out var sectionLength))
            {
                EmitError(writer, "snippet section not found"u8, pathBytes);
                return;
            }

            RewriteCore(((ReadOnlySpan<byte>)bytes).Slice(sectionStart, sectionLength), baseDirectory, fileCache, writer, visited, depth + 1);
        }
        finally
        {
            visited.Remove(pathKey);
        }
    }

    /// <summary>Resolves <paramref name="pathBytes"/> to file bytes via <paramref name="fileCache"/>, reading from disk on cache miss after a path-escape check.</summary>
    /// <param name="pathBytes">UTF-8 path bytes.</param>
    /// <param name="baseDirectory">Snippet root.</param>
    /// <param name="fileCache">Build-scoped byte-keyed cache.</param>
    /// <param name="writer">Sink (used to emit error stubs on miss).</param>
    /// <param name="bytes">Resolved file bytes on success.</param>
    /// <returns>True when the file was resolved (cache hit or successful read); false when an error stub was emitted.</returns>
    private static bool TryGetSnippetBytes(ReadOnlySpan<byte> pathBytes, DirectoryPath baseDirectory, Dictionary<byte[], byte[]> fileCache, IBufferWriter<byte> writer, out byte[] bytes)
    {
        if (fileCache.TryGetValueByUtf8(pathBytes, out bytes!))
        {
            return true;
        }

        // Cache miss: validate + read once, then memoize so subsequent references skip the filesystem entirely.
        var path = Encoding.UTF8.GetString(pathBytes);
        var absolute = Path.GetFullPath(Path.Combine(baseDirectory, path));
        if (!IsInside(baseDirectory, absolute))
        {
            EmitError(writer, "snippet path escapes base directory"u8, pathBytes);
            bytes = [];
            return false;
        }

        if (!File.Exists(absolute))
        {
            EmitError(writer, "snippet not found"u8, pathBytes);
            bytes = [];
            return false;
        }

        bytes = File.ReadAllBytes(absolute);
        fileCache[pathBytes.ToArray()] = bytes;
        return true;
    }

    /// <summary>Writes a fenced-code error stub so missing snippets surface in the rendered page rather than failing silently.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="reason">UTF-8 reason bytes.</param>
    /// <param name="pathBytes">Snippet path that triggered the error.</param>
    private static void EmitError(IBufferWriter<byte> writer, ReadOnlySpan<byte> reason, ReadOnlySpan<byte> pathBytes)
    {
        writer.Write("\n```text\n!! "u8);
        writer.Write(reason);
        writer.Write(": "u8);
        writer.Write(pathBytes);
        writer.Write("\n```\n"u8);
    }

    /// <summary>Returns true when <paramref name="absolute"/> resolves under <paramref name="baseDirectory"/>.</summary>
    /// <param name="baseDirectory">Base directory.</param>
    /// <param name="absolute">Resolved absolute path.</param>
    /// <returns>True when the resolved path is inside the base.</returns>
    private static bool IsInside(DirectoryPath baseDirectory, string absolute)
    {
        var basePath = Path.GetFullPath(baseDirectory);
        if (!basePath.EndsWith(Path.DirectorySeparatorChar))
        {
            basePath += Path.DirectorySeparatorChar;
        }

        return absolute.StartsWith(basePath, StringComparison.Ordinal);
    }

    /// <summary>Counts leading <c> </c> / <c>\t</c> bytes in <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>The number of leading whitespace bytes.</returns>
    private static int LeadingWhitespaceLength(ReadOnlySpan<byte> line)
    {
        var p = 0;
        while (p < line.Length && line[p] is (byte)' ' or (byte)'\t')
        {
            p++;
        }

        return p;
    }

    /// <summary>Writes a single byte to <paramref name="writer"/>.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="b">Byte to write.</param>
    private static void CopyByte(IBufferWriter<byte> writer, byte b) => SnippetsByteWriter.WriteOne(writer, b);
}
