// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;
using NuStreamDocs.Common;

namespace NuStreamDocs.Social;

/// <summary>Streams canonical links, OpenGraph meta tags, Twitter card tags, and Schema.org JSON-LD into <c>&lt;head&gt;</c>.</summary>
internal static class SocialHeadWriter
{
    /// <summary>Gets the OpenGraph <c>property</c> attribute name bytes.</summary>
    private static ReadOnlySpan<byte> PropertyAttribute => "property"u8;

    /// <summary>Gets the Twitter <c>name</c> attribute name bytes.</summary>
    private static ReadOnlySpan<byte> NameAttribute => "name"u8;

    /// <summary>Gets the JSON-LD <c>@type</c> property name bytes.</summary>
    private static ReadOnlySpan<byte> JsonLdTypeKey => "@type"u8;

    /// <summary>Gets the JSON-LD <c>name</c> property name bytes.</summary>
    private static ReadOnlySpan<byte> JsonLdNameKey => "name"u8;

    /// <summary>Writes enabled social tags into <paramref name="output"/>.</summary>
    /// <param name="output">Destination HTML buffer.</param>
    /// <param name="existingHead">Slice of HTML before <c>&lt;/head&gt;</c> used to avoid duplicate canonical tags.</param>
    /// <param name="payload">Resolved per-page values.</param>
    /// <param name="options">Social plugin options.</param>
    internal static void WriteHeadTags(
        IBufferWriter<byte> output,
        ReadOnlySpan<byte> existingHead,
        in SocialHeadPayload payload,
        in SocialOptions options)
    {
        if (options.EmitCanonicalLink && !payload.CanonicalUrl.AsSpan().IsEmpty && existingHead.IndexOf("rel=\"canonical\""u8) < 0)
        {
            output.Write("<link rel=\"canonical\" href=\""u8);
            XmlEntityEscaper.WriteEscaped(output, payload.CanonicalUrl, XmlEntityEscaper.Mode.HtmlAttribute);
            output.Write("\">\n"u8);
        }

        if (options.EmitOpenGraph)
        {
            WriteOpenGraphTags(output, payload);
        }

        if (options.EmitTwitterCard)
        {
            WriteTwitterTags(output, payload, options.TwitterSite);
        }

        if (options.EmitJsonLd)
        {
            WriteJsonLd(output, payload);
        }
    }

    /// <summary>Writes <c>og:*</c> meta tags.</summary>
    /// <param name="output">Destination sink.</param>
    /// <param name="payload">Resolved per-page payload.</param>
    private static void WriteOpenGraphTags(IBufferWriter<byte> output, in SocialHeadPayload payload)
    {
        WriteMetaTag(output, PropertyAttribute, "og:type"u8, "article"u8);
        WriteMetaTag(output, PropertyAttribute, "og:title"u8, payload.Title);

        if (!payload.Description.AsSpan().IsEmpty)
        {
            WriteMetaTag(output, PropertyAttribute, "og:description"u8, payload.Description);
        }

        if (!payload.SiteName.AsSpan().IsEmpty)
        {
            WriteMetaTag(output, PropertyAttribute, "og:site_name"u8, payload.SiteName);
        }

        if (!payload.CanonicalUrl.AsSpan().IsEmpty)
        {
            WriteMetaTag(output, PropertyAttribute, "og:url"u8, payload.CanonicalUrl);
        }

        if (payload.ImageUrl.AsSpan().IsEmpty)
        {
            return;
        }

        WriteMetaTag(output, PropertyAttribute, "og:image"u8, payload.ImageUrl);
        if (!payload.GeneratedSvgImage)
        {
            return;
        }

        WriteMetaTag(output, PropertyAttribute, "og:image:type"u8, "image/svg+xml"u8);
        WriteMetaTag(output, PropertyAttribute, "og:image:width"u8, "1200"u8);
        WriteMetaTag(output, PropertyAttribute, "og:image:height"u8, "630"u8);
    }

    /// <summary>Writes <c>twitter:*</c> meta tags.</summary>
    /// <param name="output">Destination buffer.</param>
    /// <param name="payload">Resolved card metadata.</param>
    /// <param name="twitterSite">Optional <c>@handle</c> bytes.</param>
    private static void WriteTwitterTags(
        IBufferWriter<byte> output,
        in SocialHeadPayload payload,
        ReadOnlySpan<byte> twitterSite)
    {
        var cardKind = !payload.ImageUrl.AsSpan().IsEmpty ? "summary_large_image"u8 : "summary"u8;
        WriteMetaTag(output, NameAttribute, "twitter:card"u8, cardKind);
        WriteMetaTag(output, NameAttribute, "twitter:title"u8, payload.Title);

        if (!payload.Description.AsSpan().IsEmpty)
        {
            WriteMetaTag(output, NameAttribute, "twitter:description"u8, payload.Description);
        }

        if (!twitterSite.IsEmpty)
        {
            WriteMetaTag(output, NameAttribute, "twitter:site"u8, twitterSite);
        }

        if (!payload.ImageUrl.AsSpan().IsEmpty)
        {
            WriteMetaTag(output, NameAttribute, "twitter:image"u8, payload.ImageUrl);
        }
    }

    /// <summary>Writes a Schema.org <c>TechArticle</c> JSON-LD script block using <see cref="Utf8JsonWriter"/>.</summary>
    /// <param name="output">Destination HTML writer.</param>
    /// <param name="payload">Structured data values.</param>
    private static void WriteJsonLd(IBufferWriter<byte> output, in SocialHeadPayload payload)
    {
        output.Write("<script type=\"application/ld+json\">"u8);
        using (var json = new Utf8JsonWriter(output))
        {
            json.WriteStartObject();
            json.WriteString("@context"u8, "https://schema.org"u8);
            json.WriteString(JsonLdTypeKey, "TechArticle"u8);
            json.WriteString("headline"u8, payload.Title);

            if (!payload.Description.AsSpan().IsEmpty)
            {
                json.WriteString("description"u8, payload.Description);
            }

            if (!payload.CanonicalUrl.AsSpan().IsEmpty)
            {
                json.WriteString("url"u8, payload.CanonicalUrl);
            }

            if (!payload.ImageUrl.AsSpan().IsEmpty)
            {
                json.WriteString("image"u8, payload.ImageUrl);
            }

            if (!payload.SiteAuthor.AsSpan().IsEmpty)
            {
                json.WriteStartObject("author"u8);
                json.WriteString(JsonLdTypeKey, "Person"u8);
                json.WriteString(JsonLdNameKey, payload.SiteAuthor);
                json.WriteEndObject();
            }

            if (!payload.SiteName.AsSpan().IsEmpty)
            {
                json.WriteStartObject("publisher"u8);
                json.WriteString(JsonLdTypeKey, "Organization"u8);
                json.WriteString(JsonLdNameKey, payload.SiteName);
                json.WriteEndObject();
            }

            json.WriteEndObject();
        }

        output.Write("</script>\n"u8);
    }

    /// <summary>Writes a single HTML <c>&lt;meta&gt;</c> element with escaped content.</summary>
    /// <param name="output">Destination writer.</param>
    /// <param name="keyAttribute">Either <c>property</c> or <c>name</c>.</param>
    /// <param name="keyValue">Meta tag identifier.</param>
    /// <param name="content">Unescaped UTF-8 value.</param>
    private static void WriteMetaTag(
        IBufferWriter<byte> output,
        ReadOnlySpan<byte> keyAttribute,
        ReadOnlySpan<byte> keyValue,
        ReadOnlySpan<byte> content)
    {
        output.Write("<meta "u8);
        output.Write(keyAttribute);
        output.Write("=\""u8);
        output.Write(keyValue);
        output.Write("\" content=\""u8);
        XmlEntityEscaper.WriteEscaped(output, content, XmlEntityEscaper.Mode.HtmlAttribute);
        output.Write("\">\n"u8);
    }
}
