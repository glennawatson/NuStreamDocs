// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using NuStreamDocs.Blog.Common;
using NuStreamDocs.Common;

namespace NuStreamDocs.Feed;

/// <summary>Renders RSS 2.0 and Atom 1.0 documents from a list of <see cref="BlogPost"/>s as UTF-8 bytes.</summary>
public static class FeedWriter
{
    /// <summary>RFC 822 date format string used in RSS pubDate / lastBuildDate.</summary>
    private const string Rfc822Format = "R";

    /// <summary>Length of the <c>.md</c> extension stripped when computing the served URL.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Initial sink capacity (~30 entries x 256 bytes) — covers most feeds without a regrow.</summary>
    private const int InitialCapacity = 8 * 1024;

    /// <summary>Renders an RSS 2.0 document.</summary>
    /// <param name="options">Feed options.</param>
    /// <param name="posts">Posts to include.</param>
    /// <param name="generatedUtc">Generation timestamp written into the feed metadata.</param>
    /// <returns>UTF-8 XML.</returns>
    public static byte[] WriteRss(FeedOptions options, BlogPost[] posts, DateTimeOffset generatedUtc)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(posts);

        ArrayBufferWriter<byte> sink = new(InitialCapacity);
        sink.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"u8);
        sink.Write("<rss version=\"2.0\">\n  <channel>\n"u8);

        WriteElement(sink, "    "u8, "title"u8, Encoding.UTF8.GetBytes(options.Title));
        WriteElement(sink, "    "u8, "link"u8, Encoding.UTF8.GetBytes(options.SiteUrl));
        WriteElement(sink, "    "u8, "description"u8, Encoding.UTF8.GetBytes(options.Description));
        WriteElement(sink, "    "u8, "lastBuildDate"u8, FormatDate(generatedUtc, Rfc822Format));

        var siteUrlBytes = Encoding.UTF8.GetBytes(options.SiteUrl.TrimEnd('/'));
        var limit = ResolveLimit(options, posts.Length);
        for (var i = 0; i < limit; i++)
        {
            WriteRssItem(sink, siteUrlBytes, posts[i]);
        }

        sink.Write("  </channel>\n</rss>"u8);
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Renders an Atom 1.0 document.</summary>
    /// <param name="options">Feed options.</param>
    /// <param name="posts">Posts to include.</param>
    /// <param name="generatedUtc">Generation timestamp.</param>
    /// <returns>UTF-8 XML.</returns>
    public static byte[] WriteAtom(FeedOptions options, BlogPost[] posts, DateTimeOffset generatedUtc)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(posts);

        ArrayBufferWriter<byte> sink = new(InitialCapacity);
        sink.Write("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"u8);
        sink.Write("<feed xmlns=\"http://www.w3.org/2005/Atom\">\n"u8);

        var siteUrlBytes = Encoding.UTF8.GetBytes(options.SiteUrl);
        WriteElement(sink, "  "u8, "id"u8, siteUrlBytes);
        WriteElement(sink, "  "u8, "title"u8, Encoding.UTF8.GetBytes(options.Title));
        WriteElement(sink, "  "u8, "subtitle"u8, Encoding.UTF8.GetBytes(options.Description));
        WriteElement(sink, "  "u8, "updated"u8, FormatDate(generatedUtc, "o"));

        sink.Write("  <link href=\""u8);
        XmlEntityEscaper.WriteEscaped(sink, siteUrlBytes, XmlEntityEscaper.Mode.HtmlAttribute);
        sink.Write("\" />\n"u8);

        var trimmedSiteUrl = Encoding.UTF8.GetBytes(options.SiteUrl.TrimEnd('/'));
        var limit = ResolveLimit(options, posts.Length);
        for (var i = 0; i < limit; i++)
        {
            WriteAtomEntry(sink, trimmedSiteUrl, posts[i]);
        }

        sink.Write("</feed>"u8);
        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Returns the effective number of items to render given the option cap.</summary>
    /// <param name="options">Feed options.</param>
    /// <param name="postCount">Available post count.</param>
    /// <returns>Effective limit.</returns>
    private static int ResolveLimit(FeedOptions options, int postCount) =>
        options.MaxItems <= 0 ? postCount : Math.Min(options.MaxItems, postCount);

    /// <summary>Writes one <c>&lt;name&gt;value&lt;/name&gt;</c> element with the value XML-escaped.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="indent">Leading whitespace bytes.</param>
    /// <param name="elementName">Element local name (UTF-8).</param>
    /// <param name="value">UTF-8 element value.</param>
    private static void WriteElement(IBufferWriter<byte> sink, ReadOnlySpan<byte> indent, ReadOnlySpan<byte> elementName, ReadOnlySpan<byte> value)
    {
        sink.Write(indent);
        sink.Write("<"u8);
        sink.Write(elementName);
        sink.Write(">"u8);
        XmlEntityEscaper.WriteEscaped(sink, value, XmlEntityEscaper.Mode.Xml);
        sink.Write("</"u8);
        sink.Write(elementName);
        sink.Write(">\n"u8);
    }

    /// <summary>Encodes <paramref name="value"/> using <paramref name="format"/> against invariant culture into a fresh UTF-8 byte array.</summary>
    /// <param name="value">Source date.</param>
    /// <param name="format">Standard or custom format string accepted by <see cref="DateTimeOffset.ToString(string, IFormatProvider)"/>.</param>
    /// <returns>UTF-8 bytes; the encode happens once per element.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell",
        "S6585:Do not hardcode the format specifier",
        Justification = "Callers pass a named constant (Rfc822Format) or the standard ISO 8601 spec 'o'.")]
    private static byte[] FormatDate(DateTimeOffset value, string format) =>
        Encoding.UTF8.GetBytes(value.ToString(format, CultureInfo.InvariantCulture));

    /// <summary>Writes one RSS 2.0 <c>&lt;item&gt;</c> element.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="siteUrlBytes">Trimmed site root.</param>
    /// <param name="post">Post.</param>
    private static void WriteRssItem(IBufferWriter<byte> sink, byte[] siteUrlBytes, BlogPost post)
    {
        var titleBytes = post.Title;
        var authorBytes = post.Author;
        var excerptBytes = post.Excerpt;
        var tagBytes = post.Tags;
        var linkBytes = BuildLinkBytes(siteUrlBytes, post.RelativePath);

        sink.Write("    <item>\n"u8);
        WriteElement(sink, "      "u8, "title"u8, titleBytes);
        WriteElement(sink, "      "u8, "link"u8, linkBytes);
        WriteElement(sink, "      "u8, "guid"u8, linkBytes);
        if (authorBytes is [_, ..])
        {
            WriteElement(sink, "      "u8, "author"u8, authorBytes);
        }

        WriteElement(sink, "      "u8, "pubDate"u8, FormatDate(new(post.Published.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)), Rfc822Format));

        for (var i = 0; i < tagBytes.Length; i++)
        {
            WriteElement(sink, "      "u8, "category"u8, tagBytes[i]);
        }

        if (excerptBytes is [_, ..])
        {
            WriteElement(sink, "      "u8, "description"u8, excerptBytes);
        }

        sink.Write("    </item>\n"u8);
    }

    /// <summary>Writes one Atom 1.0 <c>&lt;entry&gt;</c> element.</summary>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="siteUrlBytes">Trimmed site root.</param>
    /// <param name="post">Post.</param>
    private static void WriteAtomEntry(IBufferWriter<byte> sink, byte[] siteUrlBytes, BlogPost post)
    {
        var titleBytes = post.Title;
        var authorBytes = post.Author;
        var excerptBytes = post.Excerpt;
        var linkBytes = BuildLinkBytes(siteUrlBytes, post.RelativePath);

        sink.Write("  <entry>\n"u8);
        WriteElement(sink, "    "u8, "id"u8, linkBytes);
        WriteElement(sink, "    "u8, "title"u8, titleBytes);

        sink.Write("    <link href=\""u8);
        XmlEntityEscaper.WriteEscaped(sink, linkBytes, XmlEntityEscaper.Mode.HtmlAttribute);
        sink.Write("\" />\n"u8);

        WriteElement(sink, "    "u8, "updated"u8, FormatDate(new(post.Published.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)), "o"));

        if (authorBytes is [_, ..])
        {
            sink.Write("    <author>\n"u8);
            WriteElement(sink, "      "u8, "name"u8, authorBytes);
            sink.Write("    </author>\n"u8);
        }

        if (excerptBytes is [_, ..])
        {
            WriteElement(sink, "    "u8, "summary"u8, excerptBytes);
        }

        sink.Write("  </entry>\n"u8);
    }

    /// <summary>Builds an absolute URL from the trimmed site root and the post's relative <c>.md</c> path, as UTF-8 bytes.</summary>
    /// <param name="siteUrlBytes">Trimmed site root (no trailing <c>/</c>).</param>
    /// <param name="relativePath">Post relative path.</param>
    /// <returns>Absolute URL bytes.</returns>
    private static byte[] BuildLinkBytes(byte[] siteUrlBytes, FilePath relativePath)
    {
        var rel = relativePath.Value ?? string.Empty;
        var endsMd = rel.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var pageLength = endsMd ? rel.Length - MarkdownExtensionLength + ".html".Length : rel.Length;
        var pageBytes = new byte[pageLength];
        var written = endsMd
            ? Encoding.UTF8.GetBytes(rel.AsSpan(0, rel.Length - MarkdownExtensionLength), pageBytes)
            : Encoding.UTF8.GetBytes(rel.AsSpan(), pageBytes);
        if (endsMd)
        {
            ".html"u8.CopyTo(pageBytes.AsSpan(written));
        }

        return Utf8Concat.Concat(siteUrlBytes, "/"u8, pageBytes);
    }
}
