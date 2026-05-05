// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Blog.Common;

/// <summary>
/// Renders a blog index page (Markdown) listing every post, plus
/// per-tag archive pages.
/// </summary>
/// <remarks>
/// Produces Markdown so the rest of the build pipeline (CommonMark
/// scanner, plugin hooks, theme wrapping) treats blog index pages
/// just like author-written pages. Posts are listed newest first;
/// tags are emitted in alphabetical order; each post entry shows
/// title (linked), publish date, author, and excerpt.
/// </remarks>
public static class BlogIndexEmitter
{
    /// <summary>Length of the <c>.md</c> extension stripped when computing post URLs.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Length of the replacement <c>.html</c> extension.</summary>
    private const int HtmlExtensionLength = 5;

    /// <summary>Date format used in the post summary line.</summary>
    private const string PublishedDateFormat = "yyyy-MM-dd";

    /// <summary>Initial byte-capacity hint for an emitted blog index page.</summary>
    private const int IndexInitialCapacity = 2 * 1024;

    /// <summary>Initial byte-capacity hint for an emitted archive page.</summary>
    private const int ArchiveInitialCapacity = 1024;

    /// <summary>Writes the blog index file bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="title">UTF-8 page heading bytes, e.g. <c>"Blog"u8</c> or <c>"Announcements"u8</c>.</param>
    /// <param name="posts">Posts to list. Caller controls ordering.</param>
    public static void WriteIndex(IBufferWriter<byte> writer, ReadOnlySpan<byte> title, BlogPost[] posts)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (title.IsEmpty)
        {
            throw new ArgumentException("Title bytes must be non-empty.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(posts);

        writer.Write("# "u8);
        writer.Write(title);
        writer.Write("\n\n"u8);

        if (posts.Length == 0)
        {
            writer.Write("_No posts yet._\n"u8);
            return;
        }

        for (var i = 0; i < posts.Length; i++)
        {
            AppendPostSummary(writer, posts[i]);
        }
    }

    /// <summary>Writes the tag archive page bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="posts">Posts that carry the tag.</param>
    public static void WriteTagArchive(IBufferWriter<byte> writer, byte[] tag, BlogPost[] posts)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(tag);
        if (tag.Length is 0)
        {
            throw new ArgumentException("Tag must be non-empty.", nameof(tag));
        }

        ArgumentNullException.ThrowIfNull(posts);

        writer.Write("# Posts tagged \""u8);
        Utf8StringWriter.Write(writer, tag);
        writer.Write("\"\n\n"u8);

        for (var i = 0; i < posts.Length; i++)
        {
            AppendPostSummary(writer, posts[i]);
        }
    }

    /// <summary>Creates a reusable writer sized for an index page.</summary>
    /// <returns>The writer.</returns>
    public static ArrayBufferWriter<byte> CreateIndexWriter() => new(IndexInitialCapacity);

    /// <summary>Creates a reusable writer sized for an archive page.</summary>
    /// <returns>The writer.</returns>
    public static ArrayBufferWriter<byte> CreateArchiveWriter() => new(ArchiveInitialCapacity);

    /// <summary>Appends one post's summary block to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="post">Post.</param>
    [SuppressMessage(
        "Major Code Smell",
        "S6585:Do not hardcode the format specifier",
        Justification = "PublishedDateFormat is a named constant documented at its declaration.")]
    private static void AppendPostSummary(IBufferWriter<byte> writer, BlogPost post)
    {
        writer.Write("## ["u8);
        Utf8StringWriter.Write(writer, post.Title);
        writer.Write("]("u8);
        Utf8StringWriter.Write(writer, LinkPath(post.RelativePath));
        writer.Write(")\n_"u8);
        WriteDate(writer, post.Published);

        if (post.Author is [_, ..])
        {
            writer.Write(" — "u8);
            Utf8StringWriter.Write(writer, post.Author);
        }

        AppendTags(writer, post.Tags);

        writer.Write("_\n\n"u8);

        if (post.Excerpt is [])
        {
            return;
        }

        Utf8StringWriter.Write(writer, post.Excerpt);
        writer.Write("\n\n"u8);
    }

    /// <summary>Appends the tag suffix to a post-summary line when at least one tag is present.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="tags">Tag list.</param>
    private static void AppendTags(IBufferWriter<byte> writer, byte[][] tags)
    {
        if (tags is not [_, ..])
        {
            return;
        }

        writer.Write(" — "u8);
        for (var t = 0; t < tags.Length; t++)
        {
            if (t > 0)
            {
                writer.Write(", "u8);
            }

            writer.Write("`"u8);
            Utf8StringWriter.Write(writer, tags[t]);
            writer.Write("`"u8);
        }
    }

    /// <summary>Translates a <c>.md</c> source-relative path to the served URL.</summary>
    /// <param name="relativePath">Source relative path.</param>
    /// <returns>Page-relative URL.</returns>
    private static string LinkPath(string relativePath) =>
        relativePath.AsSpan().EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? string.Create(relativePath.Length + HtmlExtensionLength - MarkdownExtensionLength, relativePath, static (dst, state) =>
            {
                var src = state.AsSpan();
                var stem = src[..^MarkdownExtensionLength];
                stem.CopyTo(dst);
                ".html".AsSpan().CopyTo(dst[stem.Length..]);
            })
            : relativePath;

/// <summary>Formats <paramref name="published"/> directly into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="published">Date to write.</param>
    [SuppressMessage(
        "Major Code Smell",
        "S6585:Do not hardcode the format specifier",
        Justification = "PublishedDateFormat is a named constant documented at its declaration.")]
    private static void WriteDate(IBufferWriter<byte> writer, in DateOnly published)
    {
        Span<char> chars = stackalloc char[PublishedDateFormat.Length];
        if (!published.TryFormat(chars, out var written, PublishedDateFormat, CultureInfo.InvariantCulture))
        {
            return;
        }

        var max = Encoding.UTF8.GetMaxByteCount(written);
        var dst = writer.GetSpan(max);
        var bytesWritten = Encoding.UTF8.GetBytes(chars[..written], dst);
        writer.Advance(bytesWritten);
    }
}
