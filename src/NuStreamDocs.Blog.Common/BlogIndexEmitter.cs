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
/// just like author-written pages. Each post entry renders as a
/// Material-3-style <c>&lt;article class="md-post"&gt;</c> card so the
/// list reads like a magazine; the page itself emits
/// <c>hide: [toc]</c> frontmatter so the right-side TOC sidebar
/// disappears for catalogue pages.
/// </remarks>
public static class BlogIndexEmitter
{
    /// <summary>ISO date format used in the <c>datetime</c> attribute.</summary>
    private const string IsoDateFormat = "yyyy-MM-dd";

    /// <summary>Long display date format used as the visible <c>&lt;time&gt;</c> text.</summary>
    private const string LongDateFormat = "MMMM d, yyyy";

    /// <summary>Initial byte-capacity hint for an emitted blog index page.</summary>
    private const int IndexInitialCapacity = 2 * 1024;

    /// <summary>Initial byte-capacity hint for an emitted archive page.</summary>
    private const int ArchiveInitialCapacity = 1024;

    /// <summary>Gets the frontmatter block prefixed to every emitted index/archive page so the theme hides the right-side TOC sidebar.</summary>
    private static ReadOnlySpan<byte> HideTocFrontmatter => "---\nhide:\n  - toc\n---\n\n"u8;

    /// <summary>Writes the blog index file bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="title">UTF-8 page heading bytes, e.g. <c>"Blog"u8</c> or <c>"Announcements"u8</c>.</param>
    /// <param name="posts">Posts to list. Caller controls ordering.</param>
    /// <param name="pageDirectoryRelativeUtf8">Forward-slashed UTF-8 bytes of the index page's directory relative to the docs root.</param>
    public static void WriteIndex(IBufferWriter<byte> writer, ReadOnlySpan<byte> title, BlogPost[] posts, ReadOnlySpan<byte> pageDirectoryRelativeUtf8)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (title.IsEmpty)
        {
            throw new ArgumentException("Title bytes must be non-empty.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(posts);

        writer.Write(HideTocFrontmatter);
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
            AppendPostCard(writer, posts[i], pageDirectoryRelativeUtf8);
        }
    }

    /// <summary>Writes the tag archive page bytes into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="posts">Posts that carry the tag.</param>
    /// <param name="pageDirectoryRelativeUtf8">Forward-slashed UTF-8 bytes of the archive page's directory relative to the docs root (e.g. <c>"articles/tags"u8</c>).</param>
    public static void WriteTagArchive(IBufferWriter<byte> writer, byte[] tag, BlogPost[] posts, ReadOnlySpan<byte> pageDirectoryRelativeUtf8)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(tag);
        if (tag.Length is 0)
        {
            throw new ArgumentException("Tag must be non-empty.", nameof(tag));
        }

        ArgumentNullException.ThrowIfNull(posts);

        writer.Write(HideTocFrontmatter);
        writer.Write("# Posts tagged \""u8);
        Utf8StringWriter.Write(writer, tag);
        writer.Write("\"\n\n"u8);

        for (var i = 0; i < posts.Length; i++)
        {
            AppendPostCard(writer, posts[i], pageDirectoryRelativeUtf8);
        }
    }

    /// <summary>Creates a reusable writer sized for an index page.</summary>
    /// <returns>The writer.</returns>
    public static ArrayBufferWriter<byte> CreateIndexWriter() => new(IndexInitialCapacity);

    /// <summary>Creates a reusable writer sized for an archive page.</summary>
    /// <returns>The writer.</returns>
    public static ArrayBufferWriter<byte> CreateArchiveWriter() => new(ArchiveInitialCapacity);

    /// <summary>Appends one post's card block to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="post">Post.</param>
    /// <param name="pageDirectoryRelativeUtf8">Forward-slashed UTF-8 bytes of the index / archive page's directory relative to the docs root.</param>
    private static void AppendPostCard(IBufferWriter<byte> writer, BlogPost post, ReadOnlySpan<byte> pageDirectoryRelativeUtf8)
    {
        writer.Write("<article class=\"md-post\">\n"u8);
        AppendCardHeader(writer, post, pageDirectoryRelativeUtf8);
        AppendCardMeta(writer, post);
        AppendOptionalParagraph(writer, post.Description, "md-post__description"u8);
        AppendOptionalParagraph(writer, post.Excerpt, "md-post__excerpt"u8);
        AppendCardTags(writer, post.Tags);
        writer.Write("</article>\n\n"u8);
    }

    /// <summary>Writes the header block (title link + optional lead).</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="post">Post.</param>
    /// <param name="pageDirectoryRelativeUtf8">Page-relative directory bytes used for link rewriting.</param>
    private static void AppendCardHeader(IBufferWriter<byte> writer, BlogPost post, ReadOnlySpan<byte> pageDirectoryRelativeUtf8)
    {
        writer.Write("  <header class=\"md-post__header\">\n"u8);
        writer.Write("    <h3 class=\"md-post__title\"><a href=\""u8);
        Utf8RelativePath.WriteRelative(writer, pageDirectoryRelativeUtf8, post.RelativeUrlUtf8);
        writer.Write("\">"u8);
        XmlEntityEscaper.WriteEscaped(writer, post.Title, XmlEntityEscaper.Mode.Xml);
        writer.Write("</a></h3>\n"u8);

        if (post.Lead is [_, ..])
        {
            writer.Write("    <p class=\"md-post__lead\">"u8);
            XmlEntityEscaper.WriteEscaped(writer, post.Lead, XmlEntityEscaper.Mode.Xml);
            writer.Write("</p>\n"u8);
        }

        writer.Write("  </header>\n"u8);
    }

    /// <summary>Writes the meta block (publish date + optional author).</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="post">Post.</param>
    private static void AppendCardMeta(IBufferWriter<byte> writer, BlogPost post)
    {
        writer.Write("  <div class=\"md-post__meta\">\n    <time datetime=\""u8);
        WriteDate(writer, post.Published, IsoDateFormat);
        writer.Write("\">"u8);
        WriteDate(writer, post.Published, LongDateFormat);
        writer.Write("</time>"u8);

        if (post.Author is [_, ..])
        {
            writer.Write("<span class=\"md-post__author\"> — "u8);
            XmlEntityEscaper.WriteEscaped(writer, post.Author, XmlEntityEscaper.Mode.Xml);
            writer.Write("</span>"u8);
        }

        writer.Write("\n  </div>\n"u8);
    }

    /// <summary>Writes a <c>&lt;p&gt;</c> with the given class when <paramref name="text"/> is non-empty.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="text">Body text bytes.</param>
    /// <param name="cssClass">CSS class bytes.</param>
    private static void AppendOptionalParagraph(IBufferWriter<byte> writer, byte[] text, ReadOnlySpan<byte> cssClass)
    {
        if (text is not [_, ..])
        {
            return;
        }

        writer.Write("  <p class=\""u8);
        writer.Write(cssClass);
        writer.Write("\">"u8);
        XmlEntityEscaper.WriteEscaped(writer, text, XmlEntityEscaper.Mode.Xml);
        writer.Write("</p>\n"u8);
    }

    /// <summary>Writes the tag pill row when at least one tag is present.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="tags">Tag list.</param>
    private static void AppendCardTags(IBufferWriter<byte> writer, byte[][] tags)
    {
        if (tags is not [_, ..])
        {
            return;
        }

        writer.Write("  <div class=\"md-post__tags\">"u8);
        for (var t = 0; t < tags.Length; t++)
        {
            writer.Write("<span class=\"md-tag\">"u8);
            XmlEntityEscaper.WriteEscaped(writer, tags[t], XmlEntityEscaper.Mode.Xml);
            writer.Write("</span>"u8);
        }

        writer.Write("</div>\n"u8);
    }

    /// <summary>Formats <paramref name="published"/> with <paramref name="format"/> directly into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="published">Date to write.</param>
    /// <param name="format">Date format string.</param>
    [SuppressMessage(
        "Major Code Smell",
        "S6585:Do not hardcode the format specifier",
        Justification = "IsoDateFormat / LongDateFormat are named constants documented at their declaration.")]
    private static void WriteDate(IBufferWriter<byte> writer, in DateOnly published, string format)
    {
        Span<char> chars = stackalloc char[64];
        if (!published.TryFormat(chars, out var written, format, CultureInfo.InvariantCulture))
        {
            return;
        }

        var max = Encoding.UTF8.GetMaxByteCount(written);
        var dst = writer.GetSpan(max);
        var bytesWritten = Encoding.UTF8.GetBytes(chars[..written], dst);
        writer.Advance(bytesWritten);
    }
}
