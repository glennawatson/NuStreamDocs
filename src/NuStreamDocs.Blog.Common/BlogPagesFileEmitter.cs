// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Emits a literate-nav <c>.pages</c> override that pins blog posts in publish-date-descending order.
/// </summary>
public static class BlogPagesFileEmitter
{
    /// <summary>Initial byte capacity for the rendered <c>.pages</c> file.</summary>
    private const int InitialCapacity = 1024;

    /// <summary>Gets the generated index filename rendered ahead of every post.</summary>
    private static ReadOnlySpan<byte> IndexEntry => "index.md"u8;

    /// <summary>Gets the <c>nav:</c> key prefix.</summary>
    private static ReadOnlySpan<byte> NavHeader => "nav:\n"u8;

    /// <summary>Gets the YAML list-item prefix (two spaces, dash, space).</summary>
    private static ReadOnlySpan<byte> ListItemPrefix => "  - "u8;

    /// <summary>Renders the <c>.pages</c> override bytes for the supplied <paramref name="posts"/>.</summary>
    /// <param name="posts">Posts already ordered newest-first by the scanner.</param>
    /// <returns>UTF-8 bytes of the rendered <c>.pages</c> file.</returns>
    public static byte[] Render(BlogPost[] posts)
    {
        ArgumentNullException.ThrowIfNull(posts);
        var writer = new ArrayBufferWriter<byte>(InitialCapacity);
        Write(writer, posts);
        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Writes the <c>.pages</c> override into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target byte buffer.</param>
    /// <param name="posts">Posts already ordered newest-first by the scanner.</param>
    public static void Write(IBufferWriter<byte> writer, BlogPost[] posts)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(posts);

        WriteSpan(writer, NavHeader);
        WriteListItem(writer, IndexEntry);

        for (var i = 0; i < posts.Length; i++)
        {
            var fileName = ExtractFileName(posts[i].RelativePath.Value);
            if (fileName.Length is 0)
            {
                continue;
            }

            WriteListItem(writer, fileName);
        }
    }

    /// <summary>Returns the trailing path segment (filename) of <paramref name="relativePath"/> as ASCII bytes.</summary>
    /// <param name="relativePath">Forward-or-backslash separated relative path.</param>
    /// <returns>Filename bytes; empty when the path is empty.</returns>
    private static byte[] ExtractFileName(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return [];
        }

        var span = relativePath.AsSpan();
        var lastSlash = span.LastIndexOfAny('/', '\\');
        var name = lastSlash < 0 ? span : span[(lastSlash + 1)..];
        var dst = new byte[name.Length];
        for (var i = 0; i < name.Length; i++)
        {
            dst[i] = (byte)name[i];
        }

        return dst;
    }

    /// <summary>Appends one <c>"  - {entry}\n"</c> list item to <paramref name="writer"/>.</summary>
    /// <param name="writer">Target buffer.</param>
    /// <param name="entry">Entry filename bytes.</param>
    private static void WriteListItem(IBufferWriter<byte> writer, ReadOnlySpan<byte> entry)
    {
        WriteSpan(writer, ListItemPrefix);
        WriteSpan(writer, entry);
        var span = writer.GetSpan(1);
        span[0] = (byte)'\n';
        writer.Advance(1);
    }

    /// <summary>Copies <paramref name="value"/> into <paramref name="writer"/>.</summary>
    /// <param name="writer">Target buffer.</param>
    /// <param name="value">Bytes to append.</param>
    private static void WriteSpan(IBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
        var span = writer.GetSpan(value.Length);
        value.CopyTo(span);
        writer.Advance(value.Length);
    }
}
