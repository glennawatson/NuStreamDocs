// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;

namespace NuStreamDocs.ContentLoader.Feed;

/// <summary>
/// Streams an RSS 2.0 / RSS 1.0 / Atom feed into <see cref="FeedItem"/> entries with a forward-only
/// <see cref="XmlReader"/> — one item is materialised at a time, never the whole document model.
/// </summary>
internal static class RssAtomReader
{
    /// <summary>Reads <paramref name="xml"/> and returns its items in document order.</summary>
    /// <param name="xml">UTF-8 feed XML.</param>
    /// <returns>The parsed items; empty when the document has none.</returns>
    /// <exception cref="ContentLoaderException">When the document is not valid XML.</exception>
    public static FeedItem[] Read(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            CloseInput = false
        };

        List<FeedItem> items = [];
        using MemoryStream stream = new(xml, writable: false);
        using XmlReader reader = XmlReader.Create(stream, settings);
        try
        {
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName is "item" or "entry")
                {
                    items.Add(ReadEntry(reader));
                }
            }
        }
        catch (XmlException ex)
        {
            throw new ContentLoaderException("Feed source is not valid XML.", ex);
        }
        catch (InvalidOperationException ex)
        {
            throw new ContentLoaderException("Feed source has content the reader cannot extract.", ex);
        }

        return [.. items];
    }

    /// <summary>Reads one <c>&lt;item&gt;</c> / <c>&lt;entry&gt;</c> element, leaving the reader on its end tag.</summary>
    /// <param name="reader">Reader positioned on the entry's start tag.</param>
    /// <returns>The extracted item.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Dispatch over the RSS/Atom child-element vocabulary; the branching tracks element names, not nested logic.")]
    [SuppressMessage(
        "Sonar Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "Dispatch over the RSS/Atom child-element vocabulary; the branching tracks element names, not nested logic.")]
    private static FeedItem ReadEntry(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            return new([], [], [], [], []);
        }

        var entryDepth = reader.Depth;
        string title = string.Empty;
        string link = string.Empty;
        string linkRel = string.Empty;
        string date = string.Empty;
        string identifier = string.Empty;
        string content = string.Empty;
        string summary = string.Empty;

        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == entryDepth))
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != entryDepth + 1)
            {
                reader.Read();
                continue;
            }

            var name = reader.LocalName;
            if (name is "title")
            {
                title = First(title, ReadElementText(reader));
            }
            else if (name is "link")
            {
                (link, linkRel) = ResolveLink(reader, link, linkRel);
            }
            else if (name is "pubDate" or "updated" or "published")
            {
                date = First(date, ReadElementText(reader));
            }
            else if (name is "guid" or "id")
            {
                identifier = First(identifier, ReadElementText(reader));
            }
            else if (name is "encoded" or "content")
            {
                content = First(content, ReadContentText(reader));
            }
            else if (name is "summary" or "description")
            {
                summary = First(summary, ReadContentText(reader));
            }
            else
            {
                reader.Skip();
            }
        }

        var body = content.Length > 0 ? content : summary;
        return new(Utf8(title), Utf8(link), Utf8(date), Utf8(identifier), Utf8(body));
    }

    /// <summary>Returns <paramref name="current"/> if it already holds text, otherwise <paramref name="candidate"/>.</summary>
    /// <param name="current">Value seen so far.</param>
    /// <param name="candidate">Newly read value.</param>
    /// <returns>The value to keep.</returns>
    private static string First(string current, string candidate) =>
        current.Length > 0 ? current : candidate;

    /// <summary>Reads the text content of a simple element, leaving the reader past its end tag.</summary>
    /// <param name="reader">Reader positioned on the element start.</param>
    /// <returns>The trimmed text.</returns>
    private static string ReadElementText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.Skip();
            return string.Empty;
        }

        return reader.ReadElementContentAsString().Trim();
    }

    /// <summary>Reads an item-body element, preferring the inner markup for an Atom <c>type="xhtml"</c> body.</summary>
    /// <param name="reader">Reader positioned on a <c>&lt;content&gt;</c> / <c>&lt;encoded&gt;</c> / <c>&lt;summary&gt;</c> / <c>&lt;description&gt;</c> element.</param>
    /// <returns>The trimmed body (decoded HTML for escaped content, inner markup for XHTML content).</returns>
    private static string ReadContentText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.Skip();
            return string.Empty;
        }

        var isXhtml = string.Equals(reader.GetAttribute("type"), "xhtml", StringComparison.OrdinalIgnoreCase);
        return (isXhtml ? reader.ReadInnerXml() : reader.ReadElementContentAsString()).Trim();
    }

    /// <summary>Picks the better link — a <c>&lt;link href&gt;</c> wins over an RSS text link of the same priority, and a non-<c>self</c> Atom link wins over a <c>self</c> one.</summary>
    /// <param name="reader">Reader positioned on a <c>&lt;link&gt;</c> element.</param>
    /// <param name="currentLink">Link kept so far (empty when none).</param>
    /// <param name="currentRel">The <c>rel</c> of the link kept so far.</param>
    /// <returns>The link and its <c>rel</c> to keep.</returns>
    private static (string Link, string Rel) ResolveLink(XmlReader reader, string currentLink, string currentRel)
    {
        var rel = reader.GetAttribute("rel") ?? string.Empty;
        var href = reader.GetAttribute("href");
        var candidate = string.IsNullOrEmpty(href) ? ReadElementText(reader) : ConsumeAndReturn(reader, href.Trim());

        if (candidate.Length == 0)
        {
            return (currentLink, currentRel);
        }

        if (currentLink.Length == 0)
        {
            return (candidate, rel);
        }

        var replace = string.Equals(currentRel, "self", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(rel, "self", StringComparison.OrdinalIgnoreCase);
        return replace ? (candidate, rel) : (currentLink, currentRel);
    }

    /// <summary>Advances the reader past the current element and returns <paramref name="value"/>.</summary>
    /// <param name="reader">Reader positioned on an element start.</param>
    /// <param name="value">Value to return.</param>
    /// <returns><paramref name="value"/>.</returns>
    private static string ConsumeAndReturn(XmlReader reader, string value)
    {
        reader.Skip();
        return value;
    }

    /// <summary>Encodes text to UTF-8, returning an empty array for an empty string.</summary>
    /// <param name="text">Source text.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] Utf8(string text) =>
        text.Length == 0 ? [] : Encoding.UTF8.GetBytes(text);
}
