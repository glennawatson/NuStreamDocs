// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NuStreamDocs.ContentLoader.Feed;

/// <summary>Parses an RSS 2.0 / RSS 1.0 / Atom feed document into <see cref="FeedItem"/> entries.</summary>
internal static class RssAtomReader
{
    /// <summary>Reads <paramref name="xml"/> and returns its items in document order.</summary>
    /// <param name="xml">UTF-8 feed XML.</param>
    /// <returns>The parsed items; empty when the document has none.</returns>
    /// <exception cref="ContentLoaderException">When the document is not valid XML.</exception>
    public static FeedItem[] Read(byte[] xml)
    {
        var document = LoadHardened(xml);
        if (document.Root is not { } root)
        {
            return [];
        }

        var entryName = string.Equals(root.Name.LocalName, "feed", StringComparison.Ordinal) ? "entry" : "item";
        List<FeedItem> items = [];

        // foreach over XElement.Descendants() — IEnumerable<XElement>, no indexed alternative.
        foreach (var element in root.Descendants())
        {
            if (string.Equals(element.Name.LocalName, entryName, StringComparison.Ordinal))
            {
                items.Add(BuildItem(element));
            }
        }

        return [.. items];
    }

    /// <summary>Loads <paramref name="xml"/> with a DTD-disabled, resolver-free reader.</summary>
    /// <param name="xml">UTF-8 XML bytes.</param>
    /// <returns>The parsed document.</returns>
    private static XDocument LoadHardened(byte[] xml)
    {
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            MaxCharactersFromEntities = 0
        };

        using MemoryStream stream = new(xml, writable: false);
        using var reader = XmlReader.Create(stream, settings);
        try
        {
            return XDocument.Load(reader);
        }
        catch (XmlException ex)
        {
            throw new ContentLoaderException("Feed source is not valid XML.", ex);
        }
    }

    /// <summary>Extracts one item / entry element into a <see cref="FeedItem"/>.</summary>
    /// <param name="element">An <c>&lt;item&gt;</c> or <c>&lt;entry&gt;</c> element.</param>
    /// <returns>The extracted item.</returns>
    [SuppressMessage(
        "Sonar Code Smell",
        "S1541:Methods should not be too complex",
        Justification = "Dispatch over the RSS/Atom child-element vocabulary; the branching tracks element names, not nested logic.")]
    [SuppressMessage(
        "Sonar Code Smell",
        "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "Dispatch over the RSS/Atom child-element vocabulary; the branching tracks element names, not nested logic.")]
    private static FeedItem BuildItem(XElement element)
    {
        XElement? title = null;
        XElement? link = null;
        XElement? date = null;
        XElement? identifier = null;
        XElement? content = null;
        XElement? summary = null;

        // foreach over XElement.Elements() — IEnumerable<XElement>, no indexed alternative.
        foreach (var child in element.Elements())
        {
            var local = child.Name.LocalName;
            if (local is "title")
            {
                title ??= child;
            }
            else if (local is "link")
            {
                link = PreferAlternateLink(link, child);
            }
            else if (local is "pubDate" or "updated" or "published")
            {
                date ??= child;
            }
            else if (local is "guid" or "id")
            {
                identifier ??= child;
            }
            else if (local is "encoded" or "content")
            {
                content ??= child;
            }
            else if (local is "summary" or "description")
            {
                summary ??= child;
            }
        }

        return new(
            Bytes(title?.Value),
            Bytes(LinkValue(link)),
            Bytes(date?.Value),
            Bytes(identifier?.Value),
            Bytes((content ?? summary)?.Value));
    }

    /// <summary>Chooses the better of two <c>&lt;link&gt;</c> elements — an Atom <c>rel="alternate"</c> link beats <c>rel="self"</c>.</summary>
    /// <param name="current">The link kept so far, or null.</param>
    /// <param name="candidate">A newly seen link element.</param>
    /// <returns>The link to keep.</returns>
    private static XElement PreferAlternateLink(XElement? current, XElement candidate)
    {
        if (current is null)
        {
            return candidate;
        }

        var currentRel = current.Attribute("rel")?.Value;
        var candidateRel = candidate.Attribute("rel")?.Value;
        if (string.Equals(currentRel, "self", StringComparison.OrdinalIgnoreCase) && !string.Equals(candidateRel, "self", StringComparison.OrdinalIgnoreCase))
        {
            return candidate;
        }

        return current;
    }

    /// <summary>Returns the canonical URL of a feed link element — the Atom <c>href</c> attribute, or the RSS element text.</summary>
    /// <param name="link">The <c>&lt;link&gt;</c> element, or null.</param>
    /// <returns>The URL, or null when absent.</returns>
    private static string? LinkValue(XElement? link)
    {
        if (link is null)
        {
            return null;
        }

        var href = link.Attribute("href")?.Value;
        return string.IsNullOrEmpty(href) ? link.Value : href;
    }

    /// <summary>Encodes trimmed text to UTF-8, or returns an empty array for null.</summary>
    /// <param name="text">Source text, or null.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] Bytes(string? text) =>
        string.IsNullOrEmpty(text) ? [] : Encoding.UTF8.GetBytes(text.Trim());
}
