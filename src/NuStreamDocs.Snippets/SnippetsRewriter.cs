// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
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
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, string baseDirectory, IBufferWriter<byte> writer)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        RewriteCore(source, baseDirectory, writer, visited, 0);
    }

    /// <summary>Recursive include implementation guarded by <paramref name="depth"/> and <paramref name="visited"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="baseDirectory">Snippet root.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="visited">Already-included files (cycle guard).</param>
    /// <param name="depth">Current recursion depth.</param>
    private static void RewriteCore(ReadOnlySpan<byte> source, string baseDirectory, IBufferWriter<byte> writer, HashSet<string> visited, int depth)
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
            if (TryParseIncludeLine(source[lineStart..lineEnd], out var path))
            {
                EmitInclude(path, baseDirectory, writer, visited, depth);
                i = lineEnd;
                continue;
            }

            writer.Write(source[lineStart..lineEnd]);
            i = lineEnd;
        }
    }

    /// <summary>Tries to parse a <c>--8&lt;-- "file"</c> directive line.</summary>
    /// <param name="line">UTF-8 bytes of the candidate line.</param>
    /// <param name="path">Captured path on success.</param>
    /// <returns>True when the line is a directive.</returns>
    private static bool TryParseIncludeLine(ReadOnlySpan<byte> line, out string path)
    {
        path = string.Empty;
        var trimmed = TrimLeadingWhitespace(line);
        if (!trimmed.StartsWith(IncludeMarker))
        {
            return false;
        }

        var afterMarker = TrimLeadingWhitespace(trimmed[IncludeMarker.Length..]);
        if (afterMarker.Length < 2 || afterMarker[0] is not (byte)'"')
        {
            return false;
        }

        var closeQuote = afterMarker[1..].IndexOf((byte)'"');
        if (closeQuote < 0)
        {
            return false;
        }

        path = Encoding.UTF8.GetString(afterMarker.Slice(1, closeQuote));
        return path.Length > 0;
    }

    /// <summary>Reads <paramref name="path"/> (relative to <paramref name="baseDirectory"/>) and recursively expands its includes into <paramref name="writer"/>.</summary>
    /// <param name="path">Snippet path.</param>
    /// <param name="baseDirectory">Snippet root.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="visited">Already-included files (cycle guard).</param>
    /// <param name="depth">Current recursion depth.</param>
    private static void EmitInclude(string path, string baseDirectory, IBufferWriter<byte> writer, HashSet<string> visited, int depth)
    {
        if (depth >= MaxIncludeDepth)
        {
            EmitError(writer, "snippet include depth exceeded", path);
            return;
        }

        var absolute = Path.GetFullPath(Path.Combine(baseDirectory, path));
        if (!IsInside(baseDirectory, absolute))
        {
            EmitError(writer, "snippet path escapes base directory", path);
            return;
        }

        if (!visited.Add(absolute))
        {
            EmitError(writer, "snippet cycle detected", path);
            return;
        }

        try
        {
            if (!File.Exists(absolute))
            {
                EmitError(writer, "snippet not found", path);
                return;
            }

            var bytes = File.ReadAllBytes(absolute);
            RewriteCore(bytes, baseDirectory, writer, visited, depth + 1);
        }
        finally
        {
            visited.Remove(absolute);
        }
    }

    /// <summary>Writes a fenced-code error stub so missing snippets surface in the rendered page rather than failing silently.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="reason">Short reason string.</param>
    /// <param name="path">Snippet path that triggered the error.</param>
    private static void EmitError(IBufferWriter<byte> writer, string reason, string path)
    {
        writer.Write("\n```text\n!! "u8);
        WriteUtf8(writer, reason);
        writer.Write(": "u8);
        WriteUtf8(writer, path);
        writer.Write("\n```\n"u8);
    }

    /// <summary>Returns true when <paramref name="absolute"/> resolves under <paramref name="baseDirectory"/>.</summary>
    /// <param name="baseDirectory">Base directory.</param>
    /// <param name="absolute">Resolved absolute path.</param>
    /// <returns>True when the resolved path is inside the base.</returns>
    private static bool IsInside(string baseDirectory, string absolute)
    {
        var basePath = Path.GetFullPath(baseDirectory);
        if (!basePath.EndsWith(Path.DirectorySeparatorChar))
        {
            basePath += Path.DirectorySeparatorChar;
        }

        return absolute.StartsWith(basePath, StringComparison.Ordinal);
    }

    /// <summary>Trims leading <c> </c> / <c>\t</c> bytes from <paramref name="line"/>.</summary>
    /// <param name="line">UTF-8 line bytes.</param>
    /// <returns>The trimmed slice.</returns>
    private static ReadOnlySpan<byte> TrimLeadingWhitespace(ReadOnlySpan<byte> line)
    {
        var p = 0;
        while (p < line.Length && line[p] is (byte)' ' or (byte)'\t')
        {
            p++;
        }

        return line[p..];
    }

    /// <summary>Writes a single byte to <paramref name="writer"/>.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="b">Byte to write.</param>
    private static void CopyByte(IBufferWriter<byte> writer, byte b) => SnippetsByteWriter.WriteOne(writer, b);

    /// <summary>Encodes a UTF-16 string as UTF-8 directly into <paramref name="writer"/>.</summary>
    /// <param name="writer">Sink.</param>
    /// <param name="value">String.</param>
    private static void WriteUtf8(IBufferWriter<byte> writer, string value)
    {
        var max = Encoding.UTF8.GetMaxByteCount(value.Length);
        var dst = writer.GetSpan(max);
        var written = Encoding.UTF8.GetBytes(value, dst);
        writer.Advance(written);
    }
}
