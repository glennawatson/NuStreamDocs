// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Html;

namespace NuStreamDocs.GitInfo;

/// <summary>UTF-8 HTML and <c>sitemap.xml</c> rewriters for <see cref="GitInfoPlugin"/>.</summary>
internal static class GitInfoHtmlRewriter
{
    /// <summary>Initial capacity for rewritten <c>sitemap.xml</c> buffers.</summary>
    private const int InitialSitemapHeadroom = 1024;

    /// <summary>Gets the closing <c>&lt;/head&gt;</c> tag.</summary>
    private static ReadOnlySpan<byte> HeadClose => "</head>"u8;

    /// <summary>Gets the closing <c>&lt;/article&gt;</c> tag.</summary>
    private static ReadOnlySpan<byte> ArticleClose => "</article>"u8;

    /// <summary>Gets the closing <c>&lt;/main&gt;</c> tag.</summary>
    private static ReadOnlySpan<byte> MainClose => "</main>"u8;

    /// <summary>Gets the closing <c>&lt;/body&gt;</c> tag.</summary>
    private static ReadOnlySpan<byte> BodyClose => "</body>"u8;

    /// <summary>Gets the opening <c>&lt;loc&gt;</c> tag in <c>sitemap.xml</c>.</summary>
    private static ReadOnlySpan<byte> LocOpen => "<loc>"u8;

    /// <summary>Gets the closing <c>&lt;/loc&gt;</c> tag in <c>sitemap.xml</c>.</summary>
    private static ReadOnlySpan<byte> LocClose => "</loc>"u8;

    /// <summary>Rewrites <paramref name="html"/> into <paramref name="output"/> with the configured meta tag and revision footer.</summary>
    /// <param name="html">Rendered page HTML.</param>
    /// <param name="metadata">Resolved page Git metadata.</param>
    /// <param name="options">Configured plugin options.</param>
    /// <param name="output">UTF-8 destination buffer.</param>
    internal static void RewritePage(
        ReadOnlySpan<byte> html,
        in GitPageMetadata metadata,
        in GitInfoOptions options,
        IBufferWriter<byte> output)
    {
        var headOffset = options.EmitMetaTag ? html.IndexOf(HeadClose) : -1;
        var bodySliceStart = headOffset >= 0 ? headOffset + HeadClose.Length : 0;
        var footerOffset = options.EmitFooter
            ? bodySliceStart + FindFooterInsertionPoint(html[bodySliceStart..])
            : -1;

        if (headOffset < 0 && footerOffset < 0)
        {
            output.Write(html);
            return;
        }

        var cursor = 0;
        if (headOffset >= 0)
        {
            output.Write(html[..headOffset]);
            WriteMetaTag(metadata.LastUpdatedIsoDate, output);
            cursor = headOffset;
        }

        if (footerOffset >= 0)
        {
            output.Write(html[cursor..footerOffset]);
            WriteFooter(metadata, options.EmitContributors, output);
            cursor = footerOffset;
        }

        output.Write(html[cursor..]);
    }

    /// <summary>Enriches <c>&lt;url&gt;&lt;loc&gt;...&lt;/loc&gt;&lt;/url&gt;</c> rows in <paramref name="sitemapXml"/> with <c>&lt;lastmod&gt;</c> elements.</summary>
    /// <param name="sitemapXml">Existing <c>sitemap.xml</c> bytes.</param>
    /// <param name="baseUrl">Normalized site URL ending with <c>/</c>.</param>
    /// <param name="urlDates">Map from relative page URL bytes to ISO-8601 date bytes.</param>
    /// <returns>Updated <c>sitemap.xml</c> bytes.</returns>
    internal static byte[] EnrichSitemapBytes(
        ReadOnlySpan<byte> sitemapXml,
        ReadOnlySpan<byte> baseUrl,
        Dictionary<byte[], byte[]> urlDates)
    {
        ArrayBufferWriter<byte> sink = new(sitemapXml.Length + InitialSitemapHeadroom);
        var lookup = urlDates.GetAlternateLookup<ReadOnlySpan<byte>>();
        var cursor = 0;

        while (cursor < sitemapXml.Length)
        {
            var openRel = sitemapXml[cursor..].IndexOf(LocOpen);
            if (openRel < 0)
            {
                sink.Write(sitemapXml[cursor..]);
                break;
            }

            var locValueStart = cursor + openRel + LocOpen.Length;
            var closeRel = sitemapXml[locValueStart..].IndexOf(LocClose);
            if (closeRel < 0)
            {
                sink.Write(sitemapXml[cursor..]);
                break;
            }

            var afterLocClose = locValueStart + closeRel + LocClose.Length;
            sink.Write(sitemapXml[cursor..afterLocClose]);

            var fullLoc = sitemapXml.Slice(locValueStart, closeRel);
            if (TryMatchUrlDate(fullLoc, baseUrl, lookup, out var isoDate))
            {
                sink.Write("<lastmod>"u8);
                XmlEntityEscaper.WriteEscaped(sink, isoDate, XmlEntityEscaper.Mode.Xml);
                sink.Write("</lastmod>"u8);
            }

            cursor = afterLocClose;
        }

        return sink.WrittenSpan.ToArray();
    }

    /// <summary>Finds the preferred insertion offset (<c>&lt;/article&gt;</c>, <c>&lt;/main&gt;</c>, <c>&lt;/body&gt;</c>, or end of fragment when no shell exists).</summary>
    /// <param name="html">Rendered HTML bytes.</param>
    /// <returns>Byte offset where the footer should be inserted.</returns>
    internal static int FindFooterInsertionPoint(ReadOnlySpan<byte> html)
    {
        var article = html.LastIndexOf(ArticleClose);
        if (article >= 0)
        {
            return article;
        }

        var main = html.LastIndexOf(MainClose);
        if (main >= 0)
        {
            return main;
        }

        var body = html.LastIndexOf(BodyClose);
        return body >= 0 ? body : html.Length;
    }

    /// <summary>Matches a <c>&lt;loc&gt;</c> value against <paramref name="baseUrl"/> and looks up its <c>&lt;lastmod&gt;</c> date.</summary>
    /// <param name="fullLoc">Full URL inside <c>&lt;loc&gt;</c>.</param>
    /// <param name="baseUrl">Normalized base URL ending in <c>/</c>.</param>
    /// <param name="lookup">Span-keyed URL-to-date lookup.</param>
    /// <param name="isoDate">Matched ISO date on success.</param>
    /// <returns><c>true</c> when a date was found.</returns>
    private static bool TryMatchUrlDate(
        ReadOnlySpan<byte> fullLoc,
        ReadOnlySpan<byte> baseUrl,
        Dictionary<byte[], byte[]>.AlternateLookup<ReadOnlySpan<byte>> lookup,
        out byte[] isoDate)
    {
        isoDate = [];
        if (!fullLoc.StartsWith(baseUrl))
        {
            return false;
        }

        var relUrl = fullLoc[baseUrl.Length..];
        if (lookup.TryGetValue(relUrl, out var direct))
        {
            isoDate = direct;
            return true;
        }

        if (!relUrl.SequenceEqual("./"u8) && !relUrl.IsEmpty)
        {
            return false;
        }

        var altKey = relUrl.IsEmpty ? "./"u8 : ReadOnlySpan<byte>.Empty;
        if (!lookup.TryGetValue(altKey, out var rootDate))
        {
            return false;
        }

        isoDate = rootDate;
        return true;
    }

    /// <summary>Writes <c>&lt;meta name="dcterms.modified" content="..."&gt;</c>.</summary>
    /// <param name="isoDate">UTF-8 date bytes.</param>
    /// <param name="output">UTF-8 destination buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteMetaTag(ReadOnlySpan<byte> isoDate, IBufferWriter<byte> output)
    {
        output.Write("<meta name=\"dcterms.modified\" content=\""u8);
        HtmlEscape.EscapeAttribute(isoDate, output);
        output.Write("\">\n"u8);
    }

    /// <summary>Writes the semantic <c>&lt;footer class="nd-git-info"&gt;</c> block.</summary>
    /// <param name="metadata">Page Git metadata.</param>
    /// <param name="emitContributors">Whether contributor names are enabled.</param>
    /// <param name="output">UTF-8 destination buffer.</param>
    private static void WriteFooter(in GitPageMetadata metadata, bool emitContributors, IBufferWriter<byte> output)
    {
        output.Write("<footer class=\"nd-git-info\"><span class=\"nd-git-info__date\">Last updated <time datetime=\""u8);
        HtmlEscape.EscapeAttribute(metadata.LastUpdatedIsoDate, output);
        output.Write("\">"u8);
        HtmlEscape.EscapeText(metadata.LastUpdatedIsoDate, output);
        output.Write("</time></span>"u8);

        if (emitContributors && metadata.Contributors.Length > 0)
        {
            output.Write("<span class=\"nd-git-info__authors\">Contributors: "u8);
            for (var i = 0; i < metadata.Contributors.Length; i++)
            {
                if (i > 0)
                {
                    output.Write(", "u8);
                }

                HtmlEscape.EscapeText(metadata.Contributors[i], output);
            }

            output.Write("</span>"u8);
        }

        output.Write("</footer>"u8);
    }
}
