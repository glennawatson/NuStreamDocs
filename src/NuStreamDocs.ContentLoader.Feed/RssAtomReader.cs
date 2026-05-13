// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;
using NuStreamDocs.Common;

namespace NuStreamDocs.ContentLoader.Feed;

/// <summary>
/// Streams an RSS 2.0 / RSS 1.0 / Atom feed into <see cref="FeedItem"/> entries with a forward-only
/// <see cref="XmlReader"/> — one item is materialised at a time, never the whole document model.
/// </summary>
internal static class RssAtomReader
{
    /// <summary>Maps a recognized entry child-element local name to the handler that folds it into the draft.</summary>
    private static readonly Dictionary<string, ElementHandler> ElementHandlers = new(StringComparer.Ordinal)
    {
        ["title"] = HandleTitle,
        ["link"] = HandleLink,
        ["pubDate"] = HandleDate,
        ["updated"] = HandleDate,
        ["published"] = HandleDate,
        ["guid"] = HandleIdentifier,
        ["id"] = HandleIdentifier,
        ["encoded"] = HandleContent,
        ["content"] = HandleContent,
        ["summary"] = HandleSummary,
        ["description"] = HandleSummary
    };

    /// <summary>Folds one recognized entry child element into the running draft, consuming it from the reader.</summary>
    /// <param name="reader">Reader positioned on the child element start.</param>
    /// <param name="draft">Running entry draft.</param>
    private delegate void ElementHandler(XmlReader reader, ref EntryDraft draft);

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
        using MemoryStream stream = new(xml, false);
        using var reader = XmlReader.Create(stream, settings);
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
    private static FeedItem ReadEntry(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            return new([], [], [], [], []);
        }

        var entryDepth = reader.Depth;
        EntryDraft draft = new([], [], [], [], [], [], []);

        reader.Read();
        while (!(reader.NodeType == XmlNodeType.EndElement && reader.Depth == entryDepth))
        {
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != entryDepth + 1)
            {
                reader.Read();
                continue;
            }

            if (ElementHandlers.TryGetValue(reader.LocalName, out var handler))
            {
                handler(reader, ref draft);
            }
            else
            {
                reader.Skip();
            }
        }

        var body = draft.Content.Length > 0 ? draft.Content : draft.Summary;
        return new(draft.Title, draft.Link, draft.Date, draft.Identifier, body);
    }

    /// <summary>Folds a <c>&lt;title&gt;</c> element into the draft.</summary>
    /// <param name="reader">Reader positioned on the element start.</param>
    /// <param name="draft">Running entry draft.</param>
    private static void HandleTitle(XmlReader reader, ref EntryDraft draft) =>
        draft.Title = First(draft.Title, ReadElementText(reader));

    /// <summary>Folds a <c>&lt;link&gt;</c> element into the draft.</summary>
    /// <param name="reader">Reader positioned on the element start.</param>
    /// <param name="draft">Running entry draft.</param>
    private static void HandleLink(XmlReader reader, ref EntryDraft draft) =>
        (draft.Link, draft.LinkRel) = ResolveLink(reader, draft.Link, draft.LinkRel);

    /// <summary>Folds a date element (<c>pubDate</c> / <c>updated</c> / <c>published</c>) into the draft.</summary>
    /// <param name="reader">Reader positioned on the element start.</param>
    /// <param name="draft">Running entry draft.</param>
    private static void HandleDate(XmlReader reader, ref EntryDraft draft) =>
        draft.Date = First(draft.Date, ReadElementText(reader));

    /// <summary>Folds an identifier element (<c>guid</c> / <c>id</c>) into the draft.</summary>
    /// <param name="reader">Reader positioned on the element start.</param>
    /// <param name="draft">Running entry draft.</param>
    private static void HandleIdentifier(XmlReader reader, ref EntryDraft draft) =>
        draft.Identifier = First(draft.Identifier, ReadElementText(reader));

    /// <summary>Folds a body-content element (<c>encoded</c> / <c>content</c>) into the draft.</summary>
    /// <param name="reader">Reader positioned on the element start.</param>
    /// <param name="draft">Running entry draft.</param>
    private static void HandleContent(XmlReader reader, ref EntryDraft draft) =>
        draft.Content = First(draft.Content, ReadContentText(reader));

    /// <summary>Folds a summary element (<c>summary</c> / <c>description</c>) into the draft.</summary>
    /// <param name="reader">Reader positioned on the element start.</param>
    /// <param name="draft">Running entry draft.</param>
    private static void HandleSummary(XmlReader reader, ref EntryDraft draft) =>
        draft.Summary = First(draft.Summary, ReadContentText(reader));

    /// <summary>Returns <paramref name="current"/> if it already holds text, otherwise <paramref name="candidate"/>.</summary>
    /// <param name="current">Value seen so far.</param>
    /// <param name="candidate">Newly read value.</param>
    /// <returns>The value to keep.</returns>
    private static byte[] First(byte[] current, byte[] candidate) =>
        current.Length > 0 ? current : candidate;

    /// <summary>Reads the text content of a simple element, leaving the reader past its end tag.</summary>
    /// <param name="reader">Reader positioned on the element start.</param>
    /// <returns>The trimmed text as UTF-8 bytes.</returns>
    private static byte[] ReadElementText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.Skip();
            return [];
        }

        return Utf8(reader.ReadElementContentAsString().Trim());
    }

    /// <summary>Reads an item-body element, preferring the inner markup for an Atom <c>type="xhtml"</c> body.</summary>
    /// <param name="reader">Reader positioned on a <c>&lt;content&gt;</c> / <c>&lt;encoded&gt;</c> / <c>&lt;summary&gt;</c> / <c>&lt;description&gt;</c> element.</param>
    /// <returns>The trimmed body as UTF-8 bytes (decoded HTML for escaped content, inner markup for XHTML content).</returns>
    private static byte[] ReadContentText(XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            reader.Skip();
            return [];
        }

        var isXhtml = string.Equals(reader.GetAttribute("type"), "xhtml", StringComparison.OrdinalIgnoreCase);
        return Utf8((isXhtml ? reader.ReadInnerXml() : reader.ReadElementContentAsString()).Trim());
    }

    /// <summary>Picks the better link — a <c>&lt;link href&gt;</c> wins over an RSS text link of the same priority, and a non-<c>self</c> Atom link wins over a <c>self</c> one.</summary>
    /// <param name="reader">Reader positioned on a <c>&lt;link&gt;</c> element.</param>
    /// <param name="currentLink">Link kept so far (empty when none).</param>
    /// <param name="currentRel">The <c>rel</c> of the link kept so far.</param>
    /// <returns>The link and its <c>rel</c> to keep, as UTF-8 bytes.</returns>
    private static (byte[] Link, byte[] Rel) ResolveLink(XmlReader reader, byte[] currentLink, byte[] currentRel)
    {
        var rel = Utf8(reader.GetAttribute("rel") ?? string.Empty);
        var href = reader.GetAttribute("href");
        var candidate = string.IsNullOrEmpty(href) ? ReadElementText(reader) : ConsumeAndReturn(reader, Utf8(href.Trim()));

        if (candidate.Length == 0)
        {
            return (currentLink, currentRel);
        }

        if (currentLink.Length == 0)
        {
            return (candidate, rel);
        }

        var replace = AsciiByteHelpers.EqualsIgnoreAsciiCase(currentRel, "self"u8)
                      && !AsciiByteHelpers.EqualsIgnoreAsciiCase(rel, "self"u8);
        return replace ? (candidate, rel) : (currentLink, currentRel);
    }

    /// <summary>Advances the reader past the current element and returns <paramref name="value"/>.</summary>
    /// <param name="reader">Reader positioned on an element start.</param>
    /// <param name="value">Value to return.</param>
    /// <returns><paramref name="value"/>.</returns>
    private static byte[] ConsumeAndReturn(XmlReader reader, byte[] value)
    {
        reader.Skip();
        return value;
    }

    /// <summary>Encodes text to UTF-8, returning an empty array for an empty string.</summary>
    /// <param name="text">Source text.</param>
    /// <returns>UTF-8 bytes.</returns>
    private static byte[] Utf8(string text) =>
        text.Length == 0 ? [] : Encoding.UTF8.GetBytes(text);

    /// <summary>Mutable accumulator for the fields gathered while scanning one feed entry's child elements.</summary>
    /// <param name="Title">Entry title text bytes.</param>
    /// <param name="Link">Chosen entry link bytes.</param>
    /// <param name="LinkRel">The <c>rel</c> of the chosen link, as bytes.</param>
    /// <param name="Date">Entry publication / update date text bytes.</param>
    /// <param name="Identifier">Entry identifier bytes (<c>guid</c> / <c>id</c>).</param>
    /// <param name="Content">Full-content body text bytes.</param>
    /// <param name="Summary">Summary / description body text bytes.</param>
    private record struct EntryDraft(
        byte[] Title,
        byte[] Link,
        byte[] LinkRel,
        byte[] Date,
        byte[] Identifier,
        byte[] Content,
        byte[] Summary);
}
