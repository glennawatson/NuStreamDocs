// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.I18n;

/// <summary>Enriches <c>sitemap.xml</c> with <c>xmlns:xhtml</c> and <c>&lt;xhtml:link rel="alternate" hreflang="..."/&gt;</c> entries.</summary>
internal static class I18nSitemapEnricher
{
    /// <summary>Initial output capacity multiplier relative to input XML size.</summary>
    private const int CapacityMultiplier = 2;

    /// <summary>Gets the <c>&lt;urlset</c> tag prefix bytes.</summary>
    private static ReadOnlySpan<byte> UrlSetOpen => "<urlset"u8;

    /// <summary>Gets the <c>&lt;loc&gt;</c> open tag bytes.</summary>
    private static ReadOnlySpan<byte> LocOpen => "<loc>"u8;

    /// <summary>Gets the <c>&lt;/loc&gt;</c> close tag bytes.</summary>
    private static ReadOnlySpan<byte> LocClose => "</loc>"u8;

    /// <summary>Rewrites <paramref name="sitemapXml"/> by injecting <c>xhtml:link</c> siblings after matching <c>&lt;loc&gt;</c> elements.</summary>
    /// <param name="sitemapXml">Existing <c>sitemap.xml</c> UTF-8 bytes.</param>
    /// <param name="alternatesByLocUrl">Alternate links indexed by canonical <c>&lt;loc&gt;</c> URL string.</param>
    /// <param name="defaultLocale">Configured default locale code.</param>
    /// <returns>Enriched <c>sitemap.xml</c> UTF-8 bytes.</returns>
    internal static byte[] Enrich(
        ReadOnlySpan<byte> sitemapXml,
        Dictionary<string, LocaleAlternateLink[]> alternatesByLocUrl,
        string defaultLocale)
    {
        var writer = new ArrayBufferWriter<byte>(sitemapXml.Length * CapacityMultiplier);
        var cursor = EnsureXhtmlNamespace(sitemapXml, writer);

        while (cursor < sitemapXml.Length)
        {
            var locRel = sitemapXml[cursor..].IndexOf(LocOpen);
            if (locRel < 0)
            {
                writer.Write(sitemapXml[cursor..]);
                break;
            }

            var valStart = cursor + locRel + LocOpen.Length;
            var closeRel = sitemapXml[valStart..].IndexOf(LocClose);
            if (closeRel < 0)
            {
                writer.Write(sitemapXml[cursor..]);
                break;
            }

            var afterLocClose = valStart + closeRel + LocClose.Length;
            writer.Write(sitemapXml[cursor..afterLocClose]);

            var locUrl = Encoding.UTF8.GetString(sitemapXml.Slice(valStart, closeRel));
            if (alternatesByLocUrl.TryGetValue(locUrl, out var alternates))
            {
                WriteXhtmlLinks(writer, alternates, defaultLocale);
            }

            cursor = afterLocClose;
        }

        return writer.WrittenSpan.ToArray();
    }

    /// <summary>Ensures <c>xmlns:xhtml="http://www.w3.org/1999/xhtml"</c> is present on <c>&lt;urlset&gt;</c> and returns the byte offset after the opening tag.</summary>
    /// <param name="xml">Source XML bytes.</param>
    /// <param name="writer">Destination buffer.</param>
    /// <returns>Offset to resume copying from.</returns>
    private static int EnsureXhtmlNamespace(ReadOnlySpan<byte> xml, IBufferWriter<byte> writer)
    {
        var urlsetPos = xml.IndexOf(UrlSetOpen);
        if (urlsetPos < 0)
        {
            return 0;
        }

        var closeRel = xml[urlsetPos..].IndexOf((byte)'>');
        if (closeRel < 0)
        {
            return 0;
        }

        var tagEnd = urlsetPos + closeRel;
        var openTag = xml[urlsetPos..tagEnd];
        if (openTag.IndexOf("xmlns:xhtml="u8) >= 0)
        {
            writer.Write(xml[..(tagEnd + 1)]);
            return tagEnd + 1;
        }

        writer.Write(xml[..tagEnd]);
        writer.Write(" xmlns:xhtml=\"http://www.w3.org/1999/xhtml\">"u8);
        return tagEnd + 1;
    }

    /// <summary>Writes <c>&lt;xhtml:link&gt;</c> elements for <paramref name="alternates"/> and <c>x-default</c>.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="alternates">Locale alternate links.</param>
    /// <param name="defaultLocale">Default locale code.</param>
    private static void WriteXhtmlLinks(
        IBufferWriter<byte> writer,
        LocaleAlternateLink[] alternates,
        string defaultLocale)
    {
        byte[]? defaultHref = null;
        for (var i = 0; i < alternates.Length; i++)
        {
            var alt = alternates[i];
            WriteSingleXhtmlLink(writer, Encoding.UTF8.GetBytes(alt.LocaleCode), alt.Href);
            if (string.Equals(alt.LocaleCode, defaultLocale, StringComparison.OrdinalIgnoreCase))
            {
                defaultHref = alt.Href;
            }
        }

        if (defaultHref is not null)
        {
            WriteSingleXhtmlLink(writer, "x-default"u8, defaultHref);
        }
    }

    /// <summary>Writes a single <c>&lt;xhtml:link&gt;</c> tag.</summary>
    /// <param name="writer">Destination buffer.</param>
    /// <param name="hreflang">Locale code or <c>x-default</c>.</param>
    /// <param name="href">Target URL bytes.</param>
    private static void WriteSingleXhtmlLink(
        IBufferWriter<byte> writer,
        ReadOnlySpan<byte> hreflang,
        ReadOnlySpan<byte> href)
    {
        writer.Write("\n    <xhtml:link rel=\"alternate\" hreflang=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, hreflang, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\" href=\""u8);
        XmlEntityEscaper.WriteEscaped(writer, href, XmlEntityEscaper.Mode.HtmlAttribute);
        writer.Write("\"/>"u8);
    }
}
