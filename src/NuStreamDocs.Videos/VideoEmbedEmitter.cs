// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Html;

namespace NuStreamDocs.Videos;

/// <summary>Emits responsive UTF-8 <c>&lt;div&gt;</c>-wrapped <c>&lt;iframe&gt;</c> and <c>&lt;video&gt;</c> blocks with zero heap allocations.</summary>
internal static class VideoEmbedEmitter
{
    /// <summary>Gets the default Twitch <c>parent</c> host when neither options nor site URL supplies one.</summary>
    private static ReadOnlySpan<byte> DefaultTwitchParent => "localhost"u8;

    /// <summary>Gets the HTML-escaped <c>&amp;parent=</c> query parameter prefix for Twitch embeds.</summary>
    private static ReadOnlySpan<byte> AmpParentParam => "&amp;parent="u8;

    /// <summary>Gets the closing attribute tail shared by all emitted <c>&lt;iframe&gt;</c> players.</summary>
    private static ReadOnlySpan<byte> IframeTailAttributes =>
        " referrerpolicy=\"strict-origin-when-cross-origin\" "u8
        + "allow=\"accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share\" "u8
        + "allowfullscreen></iframe>"u8;

    /// <summary>Writes the responsive HTML block for <paramref name="match"/> into <paramref name="writer"/>.</summary>
    /// <param name="match">Parsed video match.</param>
    /// <param name="customTitle">Caller-supplied accessible title from Markdown alt/title text; empty uses the provider default.</param>
    /// <param name="options">Configured video options.</param>
    /// <param name="resolvedTwitchParent">Pre-resolved Twitch parent host.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    internal static void Emit(
        in VideoMatch match,
        ReadOnlySpan<byte> customTitle,
        in VideoOptions options,
        ReadOnlySpan<byte> resolvedTwitchParent,
        IBufferWriter<byte> writer)
    {
        var containerClass = options.ContainerClass is [_, ..]
            ? options.ContainerClass
            : VideoOptions.DefaultContainerClass;

        Utf8StringWriter.Write(writer, "<div class=\""u8);
        HtmlEscape.EscapeAttribute(containerClass, writer);
        Utf8StringWriter.WriteByte(writer, (byte)' ');
        HtmlEscape.EscapeAttribute(containerClass, writer);
        Utf8StringWriter.Write(writer, "--"u8);
        Utf8StringWriter.Write(writer, ModifierSuffix(match.Kind));
        if (match.Kind is VideoProviderKind.YouTubeShorts)
        {
            Utf8StringWriter.WriteByte(writer, (byte)' ');
            HtmlEscape.EscapeAttribute(containerClass, writer);
            Utf8StringWriter.Write(writer, "--shorts"u8);
        }

        Utf8StringWriter.Write(writer, "\">"u8);

        var effectiveTitle = SelectTitle(customTitle, match.Kind);
        if (match.Kind is VideoProviderKind.Html5Video)
        {
            EmitHtml5Video(match, effectiveTitle, writer);
        }
        else
        {
            EmitIframe(match, effectiveTitle, options, resolvedTwitchParent, writer);
        }

        Utf8StringWriter.Write(writer, "</div>"u8);
    }

    /// <summary>Emits a responsive HTML5 <c>&lt;video&gt;</c> element.</summary>
    /// <param name="match">Parsed HTML5 video match.</param>
    /// <param name="title">Accessible title attribute value.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void EmitHtml5Video(
        in VideoMatch match,
        ReadOnlySpan<byte> title,
        IBufferWriter<byte> writer)
    {
        Utf8StringWriter.Write(writer, "<video controls preload=\"metadata\" playsinline title=\""u8);
        HtmlEscape.EscapeAttribute(title, writer);
        if (match.Secondary.IsEmpty)
        {
            Utf8StringWriter.Write(writer, "\" src=\""u8);
            HtmlEscape.EscapeAttribute(match.Primary, writer);
            Utf8StringWriter.Write(writer, "\"></video>"u8);
            return;
        }

        Utf8StringWriter.Write(writer, "\"><source src=\""u8);
        HtmlEscape.EscapeAttribute(match.Primary, writer);
        Utf8StringWriter.Write(writer, "\" type=\""u8);
        Utf8StringWriter.Write(writer, match.Secondary);
        Utf8StringWriter.Write(writer, "\"></video>"u8);
    }

    /// <summary>Emits an accessible, lazy-loadable <c>&lt;iframe&gt;</c> player element.</summary>
    /// <param name="match">Parsed video match.</param>
    /// <param name="title">Accessible title attribute value.</param>
    /// <param name="options">Configured video options.</param>
    /// <param name="resolvedTwitchParent">Pre-resolved Twitch parent host.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void EmitIframe(
        in VideoMatch match,
        ReadOnlySpan<byte> title,
        in VideoOptions options,
        ReadOnlySpan<byte> resolvedTwitchParent,
        IBufferWriter<byte> writer)
    {
        Utf8StringWriter.Write(writer, "<iframe src=\""u8);
        WriteEmbedSrc(match, options, resolvedTwitchParent, writer);
        Utf8StringWriter.Write(writer, "\" title=\""u8);
        HtmlEscape.EscapeAttribute(title, writer);
        Utf8StringWriter.WriteByte(writer, (byte)'"');
        if (options.LazyLoad)
        {
            Utf8StringWriter.Write(writer, " loading=\"lazy\""u8);
        }

        Utf8StringWriter.Write(writer, IframeTailAttributes);
    }

    /// <summary>Writes the provider-specific embed <c>src</c> URL, HTML-attribute-escaped.</summary>
    /// <param name="match">Parsed video match.</param>
    /// <param name="options">Configured video options.</param>
    /// <param name="resolvedTwitchParent">Pre-resolved Twitch parent host.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteEmbedSrc(
        in VideoMatch match,
        in VideoOptions options,
        ReadOnlySpan<byte> resolvedTwitchParent,
        IBufferWriter<byte> writer)
    {
        var twitchParent = resolvedTwitchParent.IsEmpty ? DefaultTwitchParent : resolvedTwitchParent;
        switch (match.Kind)
        {
            case VideoProviderKind.YouTubeVideo or VideoProviderKind.YouTubeShorts:
                {
                    WriteYouTubeVideoSrc(match, options.YouTubePrivacyEnhanced, writer);
                    return;
                }

            case VideoProviderKind.YouTubePlaylist:
                {
                    Utf8StringWriter.Write(
                        writer,
                        options.YouTubePrivacyEnhanced
                            ? "https://www.youtube-nocookie.com/embed/videoseries?list="u8
                            : "https://www.youtube.com/embed/videoseries?list="u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    return;
                }

            case VideoProviderKind.Vimeo:
                {
                    WriteVimeoSrc(match, options.VimeoDoNotTrack, writer);
                    return;
                }

            case VideoProviderKind.OneDrive:
                {
                    WriteOneDriveSrc(match.Query, writer);
                    return;
                }

            case VideoProviderKind.MicrosoftStreamClassic:
                {
                    Utf8StringWriter.Write(writer, "https://web.microsoftstream.com/embed/video/"u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    WriteOptionalQuery(match.Query, writer);
                    return;
                }

            default:
                {
                    WriteExtendedEmbedSrc(match, twitchParent, writer);
                    return;
                }
        }
    }

    /// <summary>Writes the embed <c>src</c> for SharePoint, Azure, Loom, Dailymotion, Twitch, Wistia, Cloudflare, Google Drive, Bilibili, and Vidyard.</summary>
    /// <param name="match">Parsed video match.</param>
    /// <param name="twitchParent">Effective Twitch parent host.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteExtendedEmbedSrc(
        in VideoMatch match,
        ReadOnlySpan<byte> twitchParent,
        IBufferWriter<byte> writer)
    {
        switch (match.Kind)
        {
            case VideoProviderKind.SharePointEmbed:
                {
                    WriteSharePointEmbedSrc(match, writer);
                    return;
                }

            case VideoProviderKind.SharePointShareLink:
                {
                    WriteSharePointShareLinkSrc(match, writer);
                    return;
                }

            case VideoProviderKind.AzureVideoIndexer:
                {
                    Utf8StringWriter.Write(writer, "https://www.videoindexer.ai/embed/player/"u8);
                    HtmlEscape.EscapeAttribute(match.Secondary, writer);
                    Utf8StringWriter.WriteByte(writer, (byte)'/');
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    Utf8StringWriter.WriteByte(writer, (byte)'/');
                    WriteOptionalQuery(match.Query, writer);
                    return;
                }

            case VideoProviderKind.AzureMediaPlayer or VideoProviderKind.DailymotionPlayer:
                {
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    return;
                }

            case VideoProviderKind.Loom:
                {
                    Utf8StringWriter.Write(writer, "https://www.loom.com/embed/"u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    WriteOptionalQuery(match.Query, writer);
                    return;
                }

            case VideoProviderKind.Dailymotion:
                {
                    Utf8StringWriter.Write(writer, "https://geo.dailymotion.com/player.html?video="u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    WriteAppendedQuery(match.Query, writer);
                    return;
                }

            default:
                {
                    WriteRemainingEmbedSrc(match, twitchParent, writer);
                    return;
                }
        }
    }

    /// <summary>Writes the embed <c>src</c> for Twitch, Wistia, Cloudflare Stream, Google Drive, Bilibili, and Vidyard.</summary>
    /// <param name="match">Parsed video match.</param>
    /// <param name="twitchParent">Effective Twitch parent host.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteRemainingEmbedSrc(
        in VideoMatch match,
        ReadOnlySpan<byte> twitchParent,
        IBufferWriter<byte> writer)
    {
        switch (match.Kind)
        {
            case VideoProviderKind.TwitchVideo:
                {
                    Utf8StringWriter.Write(writer, "https://player.twitch.tv/?video="u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    Utf8StringWriter.Write(writer, AmpParentParam);
                    HtmlEscape.EscapeAttribute(twitchParent, writer);
                    Utf8StringWriter.Write(writer, "&amp;autoplay=false"u8);
                    return;
                }

            case VideoProviderKind.TwitchClip:
                {
                    Utf8StringWriter.Write(writer, "https://clips.twitch.tv/embed?clip="u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    Utf8StringWriter.Write(writer, AmpParentParam);
                    HtmlEscape.EscapeAttribute(twitchParent, writer);
                    Utf8StringWriter.Write(writer, "&amp;autoplay=false"u8);
                    return;
                }

            case VideoProviderKind.TwitchPlayer:
                {
                    WriteTwitchPlayerSrc(match.Primary, twitchParent, writer);
                    return;
                }

            case VideoProviderKind.Wistia:
                {
                    Utf8StringWriter.Write(writer, "https://fast.wistia.net/embed/iframe/"u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    WriteOptionalQuery(match.Query, writer);
                    return;
                }

            default:
                {
                    WriteCloudflareDriveBilibiliVidyardSrc(match, writer);
                    return;
                }
        }
    }

    /// <summary>Writes a <c>player.twitch.tv</c> URL and appends <c>parent=</c> when absent.</summary>
    /// <param name="query">Existing Twitch player query string.</param>
    /// <param name="twitchParent">Effective Twitch parent host.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteTwitchPlayerSrc(
        ReadOnlySpan<byte> query,
        ReadOnlySpan<byte> twitchParent,
        IBufferWriter<byte> writer)
    {
        Utf8StringWriter.Write(writer, "https://player.twitch.tv/?"u8);
        HtmlEscape.EscapeAttribute(query, writer);
        if (VideoUrlParser.TryGetQueryParam(query, "parent"u8, out _))
        {
            return;
        }

        Utf8StringWriter.Write(writer, AmpParentParam);
        HtmlEscape.EscapeAttribute(twitchParent, writer);
    }

    /// <summary>Writes the embed <c>src</c> for Cloudflare Stream, Google Drive, Bilibili, and Vidyard.</summary>
    /// <param name="match">Parsed video match.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteCloudflareDriveBilibiliVidyardSrc(in VideoMatch match, IBufferWriter<byte> writer)
    {
        switch (match.Kind)
        {
            case VideoProviderKind.CloudflareCustomer:
                {
                    Utf8StringWriter.Write(writer, "https://"u8);
                    HtmlEscape.EscapeAttribute(match.Secondary, writer);
                    Utf8StringWriter.WriteByte(writer, (byte)'/');
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    Utf8StringWriter.Write(writer, "/iframe"u8);
                    WriteOptionalQuery(match.Query, writer);
                    return;
                }

            case VideoProviderKind.CloudflareGeneric:
                {
                    Utf8StringWriter.Write(writer, "https://iframe.cloudflarestream.com/"u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    WriteOptionalQuery(match.Query, writer);
                    return;
                }

            case VideoProviderKind.CloudflareVideoDelivery:
                {
                    Utf8StringWriter.Write(writer, "https://iframe.videodelivery.net/"u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    WriteOptionalQuery(match.Query, writer);
                    return;
                }

            case VideoProviderKind.GoogleDrive:
                {
                    Utf8StringWriter.Write(writer, "https://drive.google.com/file/d/"u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    Utf8StringWriter.Write(writer, "/preview"u8);
                    return;
                }

            case VideoProviderKind.BilibiliBv or VideoProviderKind.BilibiliAv or VideoProviderKind.BilibiliPlayer:
                {
                    WriteBilibiliSrc(match, writer);
                    return;
                }

            default:
                {
                    Utf8StringWriter.Write(writer, "https://play.vidyard.com/"u8);
                    HtmlEscape.EscapeAttribute(match.Primary, writer);
                    Utf8StringWriter.Write(writer, ".html"u8);
                    WriteOptionalQuery(match.Query, writer);
                    return;
                }
        }
    }

    /// <summary>Writes a YouTube video embed URL with optional <c>start=</c> and <c>list=</c> query parameters.</summary>
    /// <param name="match">Parsed YouTube match.</param>
    /// <param name="privacyEnhanced">When <c>true</c>, uses <c>youtube-nocookie.com</c>.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteYouTubeVideoSrc(in VideoMatch match, bool privacyEnhanced, IBufferWriter<byte> writer)
    {
        Utf8StringWriter.Write(
            writer,
            privacyEnhanced
                ? "https://www.youtube-nocookie.com/embed/"u8
                : "https://www.youtube.com/embed/"u8);
        HtmlEscape.EscapeAttribute(match.Primary, writer);

        var hasQuery = false;
        if (!match.Query.IsEmpty)
        {
            Utf8StringWriter.Write(writer, "?list="u8);
            HtmlEscape.EscapeAttribute(match.Query, writer);
            hasQuery = true;
        }

        if (match.StartSeconds <= 0)
        {
            return;
        }

        Utf8StringWriter.Write(writer, hasQuery ? "&amp;start="u8 : "?start="u8);
        Utf8StringWriter.WriteInt32(writer, match.StartSeconds);
    }

    /// <summary>Writes a Vimeo player URL preserving unlisted <c>h=</c>, query parameters, <c>dnt=1</c>, and <c>#t=</c> fragments.</summary>
    /// <param name="match">Parsed Vimeo match.</param>
    /// <param name="doNotTrack">When <c>true</c>, appends <c>dnt=1</c> when missing.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteVimeoSrc(in VideoMatch match, bool doNotTrack, IBufferWriter<byte> writer)
    {
        Utf8StringWriter.Write(writer, "https://player.vimeo.com/video/"u8);
        HtmlEscape.EscapeAttribute(match.Primary, writer);

        var hasQuery = false;
        if (!match.Secondary.IsEmpty && !VideoUrlParser.TryGetQueryParam(match.Query, "h"u8, out _))
        {
            Utf8StringWriter.Write(writer, "?h="u8);
            HtmlEscape.EscapeAttribute(match.Secondary, writer);
            hasQuery = true;
        }

        if (!match.Query.IsEmpty)
        {
            Utf8StringWriter.Write(writer, hasQuery ? "&amp;"u8 : "?"u8);
            HtmlEscape.EscapeAttribute(match.Query, writer);
            hasQuery = true;
        }

        if (doNotTrack && !VideoUrlParser.TryGetQueryParam(match.Query, "dnt"u8, out _))
        {
            Utf8StringWriter.Write(writer, hasQuery ? "&amp;dnt=1"u8 : "?dnt=1"u8);
        }

        if (match.Fragment.IsEmpty)
        {
            return;
        }

        Utf8StringWriter.WriteByte(writer, (byte)'#');
        HtmlEscape.EscapeAttribute(match.Fragment, writer);
    }

    /// <summary>Writes a OneDrive <c>onedrive.live.com/embed</c> URL and appends <c>em=2</c> when missing.</summary>
    /// <param name="query">Original OneDrive query string.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteOneDriveSrc(ReadOnlySpan<byte> query, IBufferWriter<byte> writer)
    {
        Utf8StringWriter.Write(writer, "https://onedrive.live.com/embed?"u8);
        HtmlEscape.EscapeAttribute(query, writer);
        if (VideoUrlParser.TryGetQueryParam(query, "em"u8, out _))
        {
            return;
        }

        Utf8StringWriter.Write(writer, "&amp;em=2"u8);
    }

    /// <summary>Writes a SharePoint <c>_layouts/15/embed.aspx</c> URL.</summary>
    /// <param name="match">Parsed SharePoint embed match.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteSharePointEmbedSrc(in VideoMatch match, IBufferWriter<byte> writer)
    {
        HtmlEscape.EscapeAttribute(match.Primary, writer);
        Utf8StringWriter.Write(writer, "/_layouts/15/embed.aspx?"u8);
        HtmlEscape.EscapeAttribute(match.Query, writer);
        if (!VideoUrlParser.TryGetQueryParam(match.Query, "uniqueid"u8, out _)
            || VideoUrlParser.TryGetQueryParam(match.Query, "embed"u8, out _))
        {
            return;
        }

        Utf8StringWriter.Write(writer, "&amp;embed=true"u8);
    }

    /// <summary>Writes a SharePoint <c>:v:</c> video link with <c>action=embedview</c>.</summary>
    /// <param name="match">Parsed SharePoint share-link match.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteSharePointShareLinkSrc(in VideoMatch match, IBufferWriter<byte> writer)
    {
        HtmlEscape.EscapeAttribute(match.Primary, writer);
        if (match.Query.IsEmpty)
        {
            Utf8StringWriter.Write(writer, "?action=embedview"u8);
            return;
        }

        Utf8StringWriter.WriteByte(writer, (byte)'?');
        HtmlEscape.EscapeAttribute(match.Query, writer);
        if (VideoUrlParser.TryGetQueryParam(match.Query, "action"u8, out _))
        {
            return;
        }

        Utf8StringWriter.Write(writer, "&amp;action=embedview"u8);
    }

    /// <summary>Writes a Bilibili <c>player.bilibili.com</c> URL.</summary>
    /// <param name="match">Parsed Bilibili match.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteBilibiliSrc(in VideoMatch match, IBufferWriter<byte> writer)
    {
        if (match.Kind is VideoProviderKind.BilibiliPlayer)
        {
            Utf8StringWriter.Write(writer, "https://player.bilibili.com/player.html?"u8);
            HtmlEscape.EscapeAttribute(match.Primary, writer);
            return;
        }

        Utf8StringWriter.Write(
            writer,
            match.Kind is VideoProviderKind.BilibiliBv
                ? "https://player.bilibili.com/player.html?bvid="u8
                : "https://player.bilibili.com/player.html?aid="u8);
        HtmlEscape.EscapeAttribute(match.Primary, writer);
        if (!match.Query.IsEmpty)
        {
            Utf8StringWriter.Write(writer, "&amp;page="u8);
            HtmlEscape.EscapeAttribute(match.Query, writer);
        }

        Utf8StringWriter.Write(writer, "&amp;autoplay=0"u8);
    }

    /// <summary>Writes <c>?{query}</c> when <paramref name="query"/> is non-empty.</summary>
    /// <param name="query">Optional query slice without leading <c>?</c>.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteOptionalQuery(ReadOnlySpan<byte> query, IBufferWriter<byte> writer)
    {
        if (query.IsEmpty)
        {
            return;
        }

        Utf8StringWriter.WriteByte(writer, (byte)'?');
        HtmlEscape.EscapeAttribute(query, writer);
    }

    /// <summary>Writes <c>&amp;amp;{query}</c> onto an already-opened query string when <paramref name="query"/> is non-empty.</summary>
    /// <param name="query">Optional query slice without leading <c>?</c>.</param>
    /// <param name="writer">Destination UTF-8 sink.</param>
    private static void WriteAppendedQuery(ReadOnlySpan<byte> query, IBufferWriter<byte> writer)
    {
        if (query.IsEmpty)
        {
            return;
        }

        Utf8StringWriter.Write(writer, "&amp;"u8);
        HtmlEscape.EscapeAttribute(query, writer);
    }

    /// <summary>Chooses the caller-supplied title when meaningful, falling back to a descriptive provider title.</summary>
    /// <param name="customTitle">Caller-supplied title bytes.</param>
    /// <param name="kind">Matched provider kind.</param>
    /// <returns>Non-empty accessible title bytes.</returns>
    private static ReadOnlySpan<byte> SelectTitle(ReadOnlySpan<byte> customTitle, VideoProviderKind kind)
    {
        var trimmed = AsciiByteHelpers.TrimAsciiWhitespace(customTitle);
        return !trimmed.IsEmpty && !AsciiByteHelpers.EqualsIgnoreAsciiCase(trimmed, "type:video"u8)
            ? trimmed
            : DefaultPrimaryTitle(kind);
    }

    /// <summary>Returns the default accessible title for primary video providers, delegating remaining kinds.</summary>
    /// <param name="kind">Matched provider kind.</param>
    /// <returns>Default UTF-8 title bytes.</returns>
    private static ReadOnlySpan<byte> DefaultPrimaryTitle(VideoProviderKind kind) =>
        kind switch
        {
            VideoProviderKind.YouTubeVideo
                or VideoProviderKind.YouTubeShorts
                or VideoProviderKind.YouTubePlaylist => "YouTube video player"u8,
            VideoProviderKind.Vimeo => "Vimeo video player"u8,
            VideoProviderKind.OneDrive => "OneDrive video player"u8,
            VideoProviderKind.MicrosoftStreamClassic
                or VideoProviderKind.SharePointEmbed
                or VideoProviderKind.SharePointShareLink => "Microsoft Stream video player"u8,
            VideoProviderKind.AzureVideoIndexer => "Azure Video Indexer player"u8,
            VideoProviderKind.AzureMediaPlayer => "Azure Media Player"u8,
            VideoProviderKind.Loom => "Loom video player"u8,
            _ => DefaultSecondaryTitle(kind),
        };

    /// <summary>Returns the default accessible title for secondary video providers.</summary>
    /// <param name="kind">Matched provider kind.</param>
    /// <returns>Default UTF-8 title bytes.</returns>
    private static ReadOnlySpan<byte> DefaultSecondaryTitle(VideoProviderKind kind) =>
        kind switch
        {
            VideoProviderKind.Dailymotion or VideoProviderKind.DailymotionPlayer => "Dailymotion video player"u8,
            VideoProviderKind.TwitchVideo
                or VideoProviderKind.TwitchClip
                or VideoProviderKind.TwitchPlayer => "Twitch video player"u8,
            VideoProviderKind.Wistia => "Wistia video player"u8,
            VideoProviderKind.CloudflareCustomer
                or VideoProviderKind.CloudflareGeneric
                or VideoProviderKind.CloudflareVideoDelivery => "Cloudflare Stream video player"u8,
            VideoProviderKind.GoogleDrive => "Google Drive video player"u8,
            VideoProviderKind.BilibiliBv
                or VideoProviderKind.BilibiliAv
                or VideoProviderKind.BilibiliPlayer => "Bilibili video player"u8,
            VideoProviderKind.Vidyard => "Vidyard video player"u8,
            _ => "Embedded video"u8,
        };

    /// <summary>Returns the BEM modifier token appended after <c>{ContainerClass}--</c>.</summary>
    /// <param name="kind">Matched provider kind.</param>
    /// <returns>Lowercase provider token.</returns>
    private static ReadOnlySpan<byte> ModifierSuffix(VideoProviderKind kind) =>
        kind switch
        {
            VideoProviderKind.YouTubeVideo
                or VideoProviderKind.YouTubeShorts
                or VideoProviderKind.YouTubePlaylist => "youtube"u8,
            VideoProviderKind.Vimeo => "vimeo"u8,
            VideoProviderKind.OneDrive => "onedrive"u8,
            VideoProviderKind.MicrosoftStreamClassic
                or VideoProviderKind.SharePointEmbed
                or VideoProviderKind.SharePointShareLink => "stream"u8,
            VideoProviderKind.AzureVideoIndexer or VideoProviderKind.AzureMediaPlayer => "azure"u8,
            VideoProviderKind.Loom => "loom"u8,
            VideoProviderKind.Dailymotion or VideoProviderKind.DailymotionPlayer => "dailymotion"u8,
            _ => SecondaryModifierSuffix(kind),
        };

    /// <summary>Returns the BEM modifier token for secondary providers.</summary>
    /// <param name="kind">Matched provider kind.</param>
    /// <returns>Lowercase provider token.</returns>
    private static ReadOnlySpan<byte> SecondaryModifierSuffix(VideoProviderKind kind) =>
        kind switch
        {
            VideoProviderKind.TwitchVideo
                or VideoProviderKind.TwitchClip
                or VideoProviderKind.TwitchPlayer => "twitch"u8,
            VideoProviderKind.Wistia => "wistia"u8,
            VideoProviderKind.CloudflareCustomer
                or VideoProviderKind.CloudflareGeneric
                or VideoProviderKind.CloudflareVideoDelivery => "cloudflare"u8,
            VideoProviderKind.GoogleDrive => "gdrive"u8,
            VideoProviderKind.BilibiliBv
                or VideoProviderKind.BilibiliAv
                or VideoProviderKind.BilibiliPlayer => "bilibili"u8,
            VideoProviderKind.Vidyard => "vidyard"u8,
            _ => "html5"u8,
        };
}
