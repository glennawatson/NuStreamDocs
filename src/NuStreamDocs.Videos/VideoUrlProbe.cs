// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.Videos;

/// <summary>Fast UTF-8 pre-filter and host classifier for video URLs.</summary>
internal static class VideoUrlProbe
{
    /// <summary>Maximum ASCII host length accepted during stack-allocated case folding.</summary>
    internal const int MaxHostLength = 253;

    /// <summary>Bytes that end the host portion of a URL inside Markdown text.</summary>
    private static readonly SearchValues<byte> HostStopBytes =
        SearchValues.Create(":/?# \t\r\n)>\"'`]"u8);

    /// <summary>Bytes that end a URL token inside Markdown text.</summary>
    private static readonly SearchValues<byte> UrlStopBytes =
        SearchValues.Create(" \t\r\n)>\"'`"u8);

    /// <summary>Gets the scheme separator bytes (<c>://</c>).</summary>
    private static ReadOnlySpan<byte> SchemeSeparator => "://"u8;

    /// <summary>Returns <c>true</c> when <paramref name="source"/> contains at least one URL whose host or file extension matches <paramref name="providers"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="providers">Enabled video providers.</param>
    /// <returns><c>true</c> when the page may contain an embeddable video URL; otherwise, <c>false</c>.</returns>
    internal static bool MayContainEmbeddableUrl(ReadOnlySpan<byte> source, VideoProviders providers)
    {
        if (providers is VideoProviders.None || source.IsEmpty)
        {
            return false;
        }

        Span<byte> lowerHost = stackalloc byte[MaxHostLength];
        var cursor = 0;
        while (true)
        {
            var rel = source[cursor..].IndexOf(SchemeSeparator);
            if (rel < 0)
            {
                return false;
            }

            var colonOffset = cursor + rel;
            cursor = colonOffset + SchemeSeparator.Length;
            if (IsCandidateEmbeddable(source, colonOffset, cursor, providers, lowerHost))
            {
                return true;
            }
        }
    }

    /// <summary>Folds ASCII uppercase letters in <paramref name="src"/> into <paramref name="dst"/>.</summary>
    /// <param name="src">Source host bytes.</param>
    /// <param name="dst">Destination span at least <paramref name="src"/> bytes long.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void FoldAsciiLower(ReadOnlySpan<byte> src, Span<byte> dst)
    {
        for (var i = 0; i < src.Length; i++)
        {
            dst[i] = AsciiByteHelpers.ToAsciiLowerByte(src[i]);
        }
    }

    /// <summary>Maps an already-lowercased host name to its <see cref="VideoProviders"/> flag, or <see cref="VideoProviders.None"/>.</summary>
    /// <param name="lowerHost">Lowercased ASCII host bytes.</param>
    /// <returns>The matching provider flag, or <see cref="VideoProviders.None"/>.</returns>
    internal static VideoProviders ClassifyLowerHost(ReadOnlySpan<byte> lowerHost)
    {
        if (IsYouTubeHost(lowerHost))
        {
            return VideoProviders.YouTube;
        }

        if (IsVimeoHost(lowerHost))
        {
            return VideoProviders.Vimeo;
        }

        if (lowerHost.SequenceEqual("onedrive.live.com"u8))
        {
            return VideoProviders.OneDrive;
        }

        if (IsSharePointOrStreamHost(lowerHost))
        {
            return VideoProviders.SharePointStream;
        }

        return IsAzureHost(lowerHost) ? VideoProviders.Azure : ClassifySecondaryHost(lowerHost);
    }

    /// <summary>Returns <c>true</c> and outputs the HTML5 MIME type when <paramref name="path"/> ends with a supported video file extension.</summary>
    /// <param name="path">URL path slice (without query or fragment).</param>
    /// <param name="mimeType">Resolved UTF-8 MIME type (empty for QuickTime <c>.mov</c> where browsers prefer sniffing).</param>
    /// <returns><c>true</c> when <paramref name="path"/> names a supported video file.</returns>
    internal static bool TryGetHtml5MimeType(ReadOnlySpan<byte> path, out ReadOnlySpan<byte> mimeType)
    {
        mimeType = default;
        if (EndsWithIgnoreAsciiCase(path, ".mp4"u8) || EndsWithIgnoreAsciiCase(path, ".m4v"u8))
        {
            mimeType = "video/mp4"u8;
            return true;
        }

        if (EndsWithIgnoreAsciiCase(path, ".webm"u8))
        {
            mimeType = "video/webm"u8;
            return true;
        }

        if (EndsWithIgnoreAsciiCase(path, ".ogv"u8))
        {
            mimeType = "video/ogg"u8;
            return true;
        }

        return EndsWithIgnoreAsciiCase(path, ".mov"u8);
    }

    /// <summary>Returns <c>true</c> when <paramref name="value"/> ends with <paramref name="lowerSuffix"/> ignoring ASCII case.</summary>
    /// <param name="value">Candidate span.</param>
    /// <param name="lowerSuffix">Lowercase ASCII suffix.</param>
    /// <returns><c>true</c> on match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool EndsWithIgnoreAsciiCase(ReadOnlySpan<byte> value, ReadOnlySpan<byte> lowerSuffix) =>
        value.Length >= lowerSuffix.Length
        && AsciiByteHelpers.StartsWithIgnoreAsciiCase(value, value.Length - lowerSuffix.Length, lowerSuffix);

    /// <summary>Extracts the host from an absolute HTTP(S) URL into <paramref name="host"/>, returning <c>false</c> when absent.</summary>
    /// <param name="url">Absolute URL bytes.</param>
    /// <param name="host">Extracted host slice without port or path.</param>
    /// <returns><c>true</c> when a non-empty host was extracted.</returns>
    internal static bool TryExtractHost(ReadOnlySpan<byte> url, out ReadOnlySpan<byte> host)
    {
        host = default;
        var schemeLen = MatchHttpSchemeLength(url);
        if (schemeLen is 0)
        {
            return false;
        }

        var rest = url[schemeLen..];
        var stop = rest.IndexOfAny(HostStopBytes);
        var slice = stop < 0 ? rest : rest[..stop];
        if (slice.IsEmpty)
        {
            return false;
        }

        host = slice;
        return true;
    }

    /// <summary>Returns the byte length of a leading <c>https://</c> or <c>http://</c> scheme (case-insensitive), or <c>0</c>.</summary>
    /// <param name="url">Candidate URL bytes.</param>
    /// <returns>Length of the matched scheme prefix, or <c>0</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int MatchHttpSchemeLength(ReadOnlySpan<byte> url)
    {
        if (AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "https://"u8))
        {
            return "https://"u8.Length;
        }

        return AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "http://"u8) ? "http://"u8.Length : 0;
    }

    /// <summary>Checks whether a single <c>://</c> occurrence belongs to an enabled video host or direct video file.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="colonOffset">Offset of <c>:</c> in <c>://</c>.</param>
    /// <param name="hostStart">Offset immediately after <c>://</c>.</param>
    /// <param name="providers">Enabled video providers.</param>
    /// <param name="lowerHost">Scratch buffer for ASCII lowercasing.</param>
    /// <returns><c>true</c> when the candidate matches <paramref name="providers"/>.</returns>
    private static bool IsCandidateEmbeddable(
        ReadOnlySpan<byte> source,
        int colonOffset,
        int hostStart,
        VideoProviders providers,
        Span<byte> lowerHost)
    {
        if (!HasHttpSchemeBefore(source, colonOffset))
        {
            return false;
        }

        var rest = source[hostStart..];
        var stop = rest.IndexOfAny(HostStopBytes);
        var hostLen = stop < 0 ? rest.Length : stop;
        if (hostLen is <= 0 or > MaxHostLength)
        {
            return false;
        }

        FoldAsciiLower(rest[..hostLen], lowerHost);
        var matchedProvider = ClassifyLowerHost(lowerHost[..hostLen]);
        if ((matchedProvider & providers) != VideoProviders.None)
        {
            return true;
        }

        return (providers & VideoProviders.Html5File) != VideoProviders.None
               && MayHaveVideoExtension(rest);
    }

    /// <summary>Classifies the remaining video providers after the top five hosts have been checked.</summary>
    /// <param name="lowerHost">Lowercased ASCII host bytes.</param>
    /// <returns>The matching provider flag, or <see cref="VideoProviders.None"/>.</returns>
    private static VideoProviders ClassifySecondaryHost(ReadOnlySpan<byte> lowerHost)
    {
        if (lowerHost.SequenceEqual("loom.com"u8) || lowerHost.SequenceEqual("www.loom.com"u8))
        {
            return VideoProviders.Loom;
        }

        if (IsDailymotionHost(lowerHost))
        {
            return VideoProviders.Dailymotion;
        }

        if (IsTwitchHost(lowerHost))
        {
            return VideoProviders.Twitch;
        }

        if (IsWistiaHost(lowerHost))
        {
            return VideoProviders.Wistia;
        }

        if (IsCloudflareStreamHost(lowerHost))
        {
            return VideoProviders.CloudflareStream;
        }

        if (lowerHost.SequenceEqual("drive.google.com"u8))
        {
            return VideoProviders.GoogleDrive;
        }

        if (IsBilibiliHost(lowerHost))
        {
            return VideoProviders.Bilibili;
        }

        return IsVidyardHost(lowerHost) ? VideoProviders.Vidyard : VideoProviders.None;
    }

    /// <summary>Checks whether the bytes immediately preceding <paramref name="colonOffset"/> form <c>http</c> or <c>https</c> (case-insensitive).</summary>
    /// <param name="source">Source buffer.</param>
    /// <param name="colonOffset">Offset of the <c>:</c> byte in <c>://</c>.</param>
    /// <returns><c>true</c> for an HTTP or HTTPS scheme.</returns>
    private static bool HasHttpSchemeBefore(ReadOnlySpan<byte> source, int colonOffset)
    {
        const int HttpLength = 4;
        const int HttpsLength = 5;
        return (colonOffset >= HttpsLength
                && AsciiByteHelpers.StartsWithIgnoreAsciiCase(source, colonOffset - HttpsLength, "https"u8))
               || (colonOffset >= HttpLength
                   && AsciiByteHelpers.StartsWithIgnoreAsciiCase(source, colonOffset - HttpLength, "http"u8));
    }

    /// <summary>Checks whether the path portion of <paramref name="restAfterScheme"/> ends in a recognized HTML5 video extension.</summary>
    /// <param name="restAfterScheme">Slice starting at the URL host.</param>
    /// <returns><c>true</c> when the URL path ends with a video extension.</returns>
    private static bool MayHaveVideoExtension(ReadOnlySpan<byte> restAfterScheme)
    {
        var urlEnd = restAfterScheme.IndexOfAny(UrlStopBytes);
        var urlBody = urlEnd < 0 ? restAfterScheme : restAfterScheme[..urlEnd];
        var queryOrFrag = urlBody.IndexOfAny((byte)'?', (byte)'#');
        var pathPart = queryOrFrag < 0 ? urlBody : urlBody[..queryOrFrag];
        return TryGetHtml5MimeType(pathPart, out _);
    }

    /// <summary>True when <paramref name="h"/> is a YouTube host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsYouTubeHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("www.youtube.com"u8)
        || h.SequenceEqual("youtube.com"u8)
        || h.SequenceEqual("youtu.be"u8)
        || h.SequenceEqual("m.youtube.com"u8)
        || h.SequenceEqual("music.youtube.com"u8)
        || h.SequenceEqual("www.youtube-nocookie.com"u8)
        || h.SequenceEqual("youtube-nocookie.com"u8);

    /// <summary>True when <paramref name="h"/> is a Vimeo host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsVimeoHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("vimeo.com"u8)
        || h.SequenceEqual("www.vimeo.com"u8)
        || h.SequenceEqual("player.vimeo.com"u8);

    /// <summary>True when <paramref name="h"/> is a Microsoft Stream or SharePoint host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsSharePointOrStreamHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("web.microsoftstream.com"u8)
        || HasSubdomainSuffix(h, ".sharepoint.com"u8);

    /// <summary>True when <paramref name="h"/> is an Azure Video Indexer, Azure Media Player, or Azure storage/CDN host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsAzureHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("www.videoindexer.ai"u8)
        || h.SequenceEqual("videoindexer.ai"u8)
        || h.SequenceEqual("vi.azure.com"u8)
        || h.SequenceEqual("amp.azure.net"u8)
        || HasSubdomainSuffix(h, ".blob.core.windows.net"u8)
        || HasSubdomainSuffix(h, ".azureedge.net"u8)
        || HasSubdomainSuffix(h, ".azurefd.net"u8);

    /// <summary>True when <paramref name="h"/> is a Dailymotion host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsDailymotionHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("www.dailymotion.com"u8)
        || h.SequenceEqual("dailymotion.com"u8)
        || h.SequenceEqual("dai.ly"u8)
        || h.SequenceEqual("geo.dailymotion.com"u8);

    /// <summary>True when <paramref name="h"/> is a Twitch host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsTwitchHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("www.twitch.tv"u8)
        || h.SequenceEqual("twitch.tv"u8)
        || h.SequenceEqual("m.twitch.tv"u8)
        || h.SequenceEqual("clips.twitch.tv"u8)
        || h.SequenceEqual("player.twitch.tv"u8);

    /// <summary>True when <paramref name="h"/> is a Wistia host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsWistiaHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("fast.wistia.net"u8)
        || h.SequenceEqual("fast.wistia.com"u8)
        || h.SequenceEqual("wistia.com"u8)
        || HasSubdomainSuffix(h, ".wistia.com"u8);

    /// <summary>True when <paramref name="h"/> is a Cloudflare Stream host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsCloudflareStreamHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("watch.cloudflarestream.com"u8)
        || h.SequenceEqual("iframe.cloudflarestream.com"u8)
        || h.SequenceEqual("videodelivery.net"u8)
        || h.SequenceEqual("iframe.videodelivery.net"u8)
        || (h.StartsWith("customer-"u8) && HasSubdomainSuffix(h, ".cloudflarestream.com"u8));

    /// <summary>True when <paramref name="h"/> is a Bilibili host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsBilibiliHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("www.bilibili.com"u8)
        || h.SequenceEqual("bilibili.com"u8)
        || h.SequenceEqual("m.bilibili.com"u8)
        || h.SequenceEqual("player.bilibili.com"u8);

    /// <summary>True when <paramref name="h"/> is a Vidyard host.</summary>
    /// <param name="h">Lowercased host.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsVidyardHost(ReadOnlySpan<byte> h) =>
        h.SequenceEqual("share.vidyard.com"u8)
        || h.SequenceEqual("play.vidyard.com"u8)
        || h.SequenceEqual("video.vidyard.com"u8);

    /// <summary>Returns <c>true</c> when <paramref name="lowerHost"/> ends with <paramref name="dotSuffix"/> and has at least one character before the dot.</summary>
    /// <param name="lowerHost">Lowercased host.</param>
    /// <param name="dotSuffix">Suffix starting with <c>.</c> (for example <c>.sharepoint.com</c>).</param>
    /// <returns><c>true</c> when a non-empty subdomain precedes <paramref name="dotSuffix"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasSubdomainSuffix(ReadOnlySpan<byte> lowerHost, ReadOnlySpan<byte> dotSuffix) =>
        lowerHost.Length > dotSuffix.Length && lowerHost.EndsWith(dotSuffix);
}
