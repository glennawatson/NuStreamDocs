// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using System.Xml;
using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Feed;

/// <summary>Renders RSS 2.0 and Atom 1.0 documents from a list of <see cref="BlogPost"/>s.</summary>
public static class FeedWriter
{
    /// <summary>RFC 822 date format string used in RSS pubDate / lastBuildDate.</summary>
    private const string Rfc822Format = "R";

    /// <summary>Length of the <c>.md</c> extension stripped when computing the served URL.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Element / attribute name constants reused across the writer.</summary>
    private const string TitleElement = "title";

    /// <summary>Element name for absolute URL.</summary>
    private const string LinkElement = "link";

    /// <summary>Element name for the description / subtitle field.</summary>
    private const string DescriptionElement = "description";

    /// <summary>Atom namespace URI.</summary>
    private const string AtomNamespace = "http://www.w3.org/2005/Atom";

    /// <summary>Renders an RSS 2.0 document.</summary>
    /// <param name="options">Feed options.</param>
    /// <param name="posts">Posts to include.</param>
    /// <param name="generatedUtc">Generation timestamp written into the feed metadata.</param>
    /// <returns>UTF-8 XML.</returns>
    public static byte[] WriteRss(FeedOptions options, BlogPost[] posts, DateTimeOffset generatedUtc)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(posts);

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new() { Indent = true, Encoding = new UTF8Encoding(false) }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("rss");
            writer.WriteAttributeString("version", "2.0");
            writer.WriteStartElement("channel");
            writer.WriteElementString(TitleElement, options.Title);
            writer.WriteElementString(LinkElement, options.SiteUrl);
            writer.WriteElementString(DescriptionElement, options.Description);
            writer.WriteElementString("lastBuildDate", generatedUtc.ToString(Rfc822Format, CultureInfo.InvariantCulture));

            var limit = ResolveLimit(options, posts.Length);
            for (var i = 0; i < limit; i++)
            {
                WriteRssItem(writer, options, posts[i]);
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return CopyStream(stream);
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

        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new() { Indent = true, Encoding = new UTF8Encoding(false) }))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("feed", AtomNamespace);
            writer.WriteElementString("id", options.SiteUrl);
            writer.WriteElementString(TitleElement, options.Title);
            writer.WriteElementString("subtitle", options.Description);
            writer.WriteElementString("updated", generatedUtc.ToString("o", CultureInfo.InvariantCulture));

            writer.WriteStartElement("link");
            writer.WriteAttributeString("href", options.SiteUrl);
            writer.WriteEndElement();

            var limit = ResolveLimit(options, posts.Length);
            for (var i = 0; i < limit; i++)
            {
                WriteAtomEntry(writer, options, posts[i]);
            }

            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return CopyStream(stream);
    }

    /// <summary>Returns the effective number of items to render given the option cap.</summary>
    /// <param name="options">Feed options.</param>
    /// <param name="postCount">Available post count.</param>
    /// <returns>Effective limit.</returns>
    private static int ResolveLimit(FeedOptions options, int postCount) =>
        options.MaxItems <= 0 ? postCount : Math.Min(options.MaxItems, postCount);

    /// <summary>Copies the written bytes of <paramref name="stream"/> into a right-sized array.</summary>
    /// <param name="stream">Completed memory stream.</param>
    /// <returns>Owned UTF-8 byte array.</returns>
    private static byte[] CopyStream(MemoryStream stream)
    {
        var length = checked((int)stream.Length);
        if (stream.TryGetBuffer(out var buffer))
        {
            return [.. buffer.AsSpan(0, length)];
        }

        var owned = new byte[length];
        stream.Position = 0;
        _ = stream.Read(owned, 0, length);
        return owned;
    }

    /// <summary>Writes one RSS 2.0 <c>&lt;item&gt;</c> element.</summary>
    /// <param name="writer">XML writer.</param>
    /// <param name="options">Options.</param>
    /// <param name="post">Post.</param>
    private static void WriteRssItem(XmlWriter writer, FeedOptions options, BlogPost post)
    {
        writer.WriteStartElement("item");
        writer.WriteElementString(TitleElement, post.Title);
        var link = BuildLink(options.SiteUrl, post.RelativePath);
        writer.WriteElementString(LinkElement, link);
        writer.WriteElementString("guid", link);
        if (!string.IsNullOrEmpty(post.Author))
        {
            writer.WriteElementString("author", post.Author);
        }

        writer.WriteElementString(
            "pubDate",
            post.Published.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString(Rfc822Format, CultureInfo.InvariantCulture));

        for (var i = 0; i < post.Tags.Length; i++)
        {
            writer.WriteElementString("category", post.Tags[i]);
        }

        if (!string.IsNullOrEmpty(post.Excerpt))
        {
            writer.WriteElementString(DescriptionElement, post.Excerpt);
        }

        writer.WriteEndElement();
    }

    /// <summary>Writes one Atom 1.0 <c>&lt;entry&gt;</c> element.</summary>
    /// <param name="writer">XML writer.</param>
    /// <param name="options">Options.</param>
    /// <param name="post">Post.</param>
    private static void WriteAtomEntry(XmlWriter writer, FeedOptions options, BlogPost post)
    {
        writer.WriteStartElement("entry");
        var link = BuildLink(options.SiteUrl, post.RelativePath);
        writer.WriteElementString("id", link);
        writer.WriteElementString(TitleElement, post.Title);

        writer.WriteStartElement("link");
        writer.WriteAttributeString("href", link);
        writer.WriteEndElement();

        writer.WriteElementString(
            "updated",
            post.Published.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture));

        if (!string.IsNullOrEmpty(post.Author))
        {
            writer.WriteStartElement("author");
            writer.WriteElementString("name", post.Author);
            writer.WriteEndElement();
        }

        if (!string.IsNullOrEmpty(post.Excerpt))
        {
            writer.WriteElementString("summary", post.Excerpt);
        }

        writer.WriteEndElement();
    }

    /// <summary>Builds an absolute URL from the site URL and the post's relative <c>.md</c> path.</summary>
    /// <param name="siteUrl">Site root.</param>
    /// <param name="relativePath">Post relative path.</param>
    /// <returns>Absolute URL.</returns>
    private static string BuildLink(string siteUrl, string relativePath)
    {
        var trimmed = siteUrl.TrimEnd('/');
        var page = relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
            ? $"{relativePath.AsSpan(0, relativePath.Length - MarkdownExtensionLength)}.html"
            : relativePath;
        return $"{trimmed}/{page}";
    }
}
