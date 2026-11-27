// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Text;
using System.Numerics;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;

namespace NuStreamDocs.Videos;

/// <summary>Zero-allocation UTF-8 URL parser for every supported video provider.</summary>
internal static class VideoUrlParser
{
    /// <summary>Bit index of <see cref="VideoProviders.YouTube"/>.</summary>
    private const int YouTubeBit = 0;

    /// <summary>Bit index of <see cref="VideoProviders.Vimeo"/>.</summary>
    private const int VimeoBit = 1;

    /// <summary>Bit index of <see cref="VideoProviders.OneDrive"/>.</summary>
    private const int OneDriveBit = 2;

    /// <summary>Bit index of <see cref="VideoProviders.SharePointStream"/>.</summary>
    private const int SharePointStreamBit = 3;

    /// <summary>Bit index of <see cref="VideoProviders.Azure"/>.</summary>
    private const int AzureBit = 4;

    /// <summary>Bit index of <see cref="VideoProviders.Loom"/>.</summary>
    private const int LoomBit = 5;

    /// <summary>Bit index of <see cref="VideoProviders.Dailymotion"/>.</summary>
    private const int DailymotionBit = 6;

    /// <summary>Bit index of <see cref="VideoProviders.Twitch"/>.</summary>
    private const int TwitchBit = 7;

    /// <summary>Bit index of <see cref="VideoProviders.Wistia"/>.</summary>
    private const int WistiaBit = 8;

    /// <summary>Bit index of <see cref="VideoProviders.CloudflareStream"/>.</summary>
    private const int CloudflareStreamBit = 9;

    /// <summary>Bit index of <see cref="VideoProviders.GoogleDrive"/>.</summary>
    private const int GoogleDriveBit = 10;

    /// <summary>Bit index of <see cref="VideoProviders.Bilibili"/>.</summary>
    private const int BilibiliBit = 11;

    /// <summary>Seconds in one minute.</summary>
    private const int SecondsPerMinute = 60;

    /// <summary>Seconds in one hour.</summary>
    private const int SecondsPerHour = 3600;

    /// <summary>Length of Bilibili's two-byte <c>BV</c> or <c>av</c> prefix.</summary>
    private const int BilibiliPrefixLength = 2;

    /// <summary>Bytes forbidden anywhere in a candidate URL.</summary>
    private static readonly SearchValues<byte> ForbiddenUrlBytes =
        SearchValues.Create(" \t\r\n<>\"'`\\"u8);

    /// <summary>Bytes valid inside a video slug, UUID, or resource identifier.</summary>
    private static readonly SearchValues<byte> SafeIdentifierBytes =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_."u8);

    /// <summary>Gets the <c>/video/</c> path prefix.</summary>
    private static ReadOnlySpan<byte> VideoPathPrefix => "/video/"u8;

    /// <summary>Gets the <c>/videos/</c> path prefix.</summary>
    private static ReadOnlySpan<byte> VideosPathPrefix => "/videos/"u8;

    /// <summary>Gets the <c>/embed/</c> path prefix.</summary>
    private static ReadOnlySpan<byte> EmbedPathPrefix => "/embed/"u8;

    /// <summary>Gets the <c>/embed/video/</c> path prefix.</summary>
    private static ReadOnlySpan<byte> EmbedVideoPathPrefix => "/embed/video/"u8;

    /// <summary>Attempts to parse <paramref name="url"/> as an embeddable video under <paramref name="providers"/>.</summary>
    /// <param name="url">Trimmed UTF-8 HTTP(S) URL.</param>
    /// <param name="providers">Enabled video providers.</param>
    /// <param name="match">Populated match slices on success.</param>
    /// <returns><c>true</c> when <paramref name="url"/> is a valid, enabled video URL; otherwise, <c>false</c>.</returns>
    internal static bool TryParse(ReadOnlySpan<byte> url, VideoProviders providers, out VideoMatch match)
    {
        match = default;
        if (providers is VideoProviders.None || url.IsEmpty || url.IndexOfAny(ForbiddenUrlBytes) >= 0)
        {
            return false;
        }

        if (!TrySplitUrl(url, out var parts) || parts.Host.Length > VideoUrlProbe.MaxHostLength)
        {
            return false;
        }

        Span<byte> lowerHostBuffer = stackalloc byte[VideoUrlProbe.MaxHostLength];
        VideoUrlProbe.FoldAsciiLower(parts.Host, lowerHostBuffer);
        ReadOnlySpan<byte> lowerHost = lowerHostBuffer[..parts.Host.Length];
        var hostProvider = VideoUrlProbe.ClassifyLowerHost(lowerHost);

        if ((hostProvider & providers) != VideoProviders.None
            && TryDispatchProvider(parts, lowerHost, hostProvider, out match))
        {
            return true;
        }

        if ((providers & VideoProviders.Html5File) == VideoProviders.None
            || !VideoUrlProbe.TryGetHtml5MimeType(parts.Path, out var mimeType))
        {
            return false;
        }

        match = new(VideoProviderKind.Html5Video, url, mimeType, default, default, -1);
        return true;
    }

    /// <summary>Returns <c>true</c> when <paramref name="query"/> contains a query parameter named <paramref name="name"/>.</summary>
    /// <param name="query">Query slice without leading <c>?</c>.</param>
    /// <param name="name">Lowercase parameter name without <c>=</c>.</param>
    /// <param name="value">Extracted parameter value slice.</param>
    /// <returns><c>true</c> when a non-empty parameter value was found.</returns>
    internal static bool TryGetQueryParam(ReadOnlySpan<byte> query, ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value)
    {
        value = default;
        var cursor = 0;
        while (cursor < query.Length)
        {
            var amp = query[cursor..].IndexOf((byte)'&');
            var pair = amp < 0 ? query[cursor..] : query.Slice(cursor, amp);
            cursor = amp < 0 ? query.Length : cursor + amp + 1;

            var eq = pair.IndexOf((byte)'=');
            if (eq != name.Length || !AsciiByteHelpers.StartsWithIgnoreAsciiCase(pair, 0, name))
            {
                continue;
            }

            var candidate = pair[(eq + 1)..];
            if (candidate.IsEmpty)
            {
                continue;
            }

            value = candidate;
            return true;
        }

        return false;
    }

    /// <summary>True when <paramref name="span"/> is non-empty and every byte is a safe video identifier character.</summary>
    /// <param name="span">Candidate identifier slice.</param>
    /// <returns><c>true</c> when valid.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsSafeId(ReadOnlySpan<byte> span) =>
        !span.IsEmpty && span.IndexOfAnyExcept(SafeIdentifierBytes) < 0;

    /// <summary>True when <paramref name="span"/> is non-empty and consists solely of ASCII decimal digits.</summary>
    /// <param name="span">Candidate numeric slice.</param>
    /// <returns><c>true</c> when every byte is <c>0</c>..<c>9</c>.</returns>
    internal static bool IsAllAsciiDigits(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
        {
            return false;
        }

        for (var i = 0; i < span.Length; i++)
        {
            if (!AsciiByteHelpers.IsAsciiDigit(span[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Dispatches a host-matched URL to its provider-specific path/query parser.</summary>
    /// <param name="parts">Decomposed URL slices referencing the original input buffer.</param>
    /// <param name="lowerHost">Stack-lowercased ASCII host span.</param>
    /// <param name="provider">Classified host provider.</param>
    /// <param name="match">Populated match slices on success.</param>
    /// <returns><c>true</c> when the URL represents a valid embeddable video.</returns>
    private static bool TryDispatchProvider(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        VideoProviders provider,
        out VideoMatch match) =>
        BitOperations.TrailingZeroCount((uint)provider) switch
        {
            YouTubeBit => TryParseYouTube(parts, lowerHost, out match),
            VimeoBit => TryParseVimeo(parts, lowerHost, out match),
            OneDriveBit => TryParseOneDrive(parts.Query, out match),
            SharePointStreamBit => TryParseSharePointOrStream(parts, lowerHost, out match),
            AzureBit => TryParseAzure(parts, lowerHost, out match),
            LoomBit => TryParseLoom(parts.Path, parts.Query, out match),
            DailymotionBit => TryParseDailymotion(parts, lowerHost, out match),
            TwitchBit => TryParseTwitch(parts, lowerHost, out match),
            WistiaBit => TryParseWistia(parts.Path, parts.Query, out match),
            CloudflareStreamBit => TryParseCloudflareStream(parts, lowerHost, out match),
            GoogleDriveBit => TryParseGoogleDrive(parts.Path, out match),
            BilibiliBit => TryParseBilibili(parts, lowerHost, out match),
            _ => TryParseVidyard(parts, lowerHost, out match),
        };

    /// <summary>Splits an HTTP(S) URL into host, path, query, and fragment slices.</summary>
    /// <param name="url">Candidate HTTP(S) URL.</param>
    /// <param name="parts">Populated decomposed slices on success.</param>
    /// <returns><c>true</c> when <paramref name="url"/> has an HTTP(S) scheme and a non-empty host.</returns>
    private static bool TrySplitUrl(ReadOnlySpan<byte> url, out ParsedUrl parts)
    {
        parts = default;
        var schemeLen = VideoUrlProbe.MatchHttpSchemeLength(url);
        if (schemeLen is 0)
        {
            return false;
        }

        var withoutScheme = url[schemeLen..];
        ReadOnlySpan<byte> fragment = default;
        var hashOffset = withoutScheme.IndexOf((byte)'#');
        if (hashOffset >= 0)
        {
            fragment = withoutScheme[(hashOffset + 1)..];
            withoutScheme = withoutScheme[..hashOffset];
        }

        ReadOnlySpan<byte> query = default;
        var questionOffset = withoutScheme.IndexOf((byte)'?');
        if (questionOffset >= 0)
        {
            query = withoutScheme[(questionOffset + 1)..];
            withoutScheme = withoutScheme[..questionOffset];
        }

        var slashOffset = withoutScheme.IndexOf((byte)'/');
        var authority = slashOffset < 0 ? withoutScheme : withoutScheme[..slashOffset];
        var path = slashOffset < 0 ? default : withoutScheme[slashOffset..];

        var colonOffset = authority.IndexOf((byte)':');
        var host = colonOffset < 0 ? authority : authority[..colonOffset];
        if (host.IsEmpty)
        {
            return false;
        }

        parts = new(url, host, path, query, fragment);
        return true;
    }

    /// <summary>Parses YouTube watch, short-domain, Shorts, live, embed, and playlist URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a YouTube video or playlist was matched.</returns>
    private static bool TryParseYouTube(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        var startSeconds = ParseYouTubeStartSeconds(parts.Query);
        _ = TryGetQueryParam(parts.Query, "list"u8, out var listId);
        if (!listId.IsEmpty && !IsSafeId(listId))
        {
            listId = default;
        }

        if (lowerHost.SequenceEqual("youtu.be"u8))
        {
            match = default;
            return TryReadSingleSegment(parts.Path, out var shortId)
                   && IsSafeId(shortId)
                   && Succeed(VideoProviderKind.YouTubeVideo, shortId, default, listId, default, startSeconds, out match);
        }

        return TryParseYouTubePath(parts.Path, listId, startSeconds, out match)
               || TryParseYouTubeQuery(parts.Path, parts.Query, listId, startSeconds, out match);
    }

    /// <summary>Parses path-based YouTube forms (<c>/shorts/</c>, <c>/embed/</c>, <c>/live/</c>, and <c>/v/</c>).</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="listId">Validated optional playlist ID.</param>
    /// <param name="startSeconds">Parsed start offset in seconds, or <c>-1</c>.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a path-based YouTube video was matched.</returns>
    private static bool TryParseYouTubePath(
        ReadOnlySpan<byte> path,
        ReadOnlySpan<byte> listId,
        int startSeconds,
        out VideoMatch match)
    {
        match = default;
        if (TryMatchPrefixSegment(path, "/shorts/"u8, out var shortsId) && IsSafeId(shortsId))
        {
            return Succeed(VideoProviderKind.YouTubeShorts, shortsId, default, default, default, startSeconds, out match);
        }

        var hasPathId = TryMatchPrefixSegment(path, EmbedPathPrefix, out var pathId)
                        || TryMatchPrefixSegment(path, "/live/"u8, out pathId)
                        || TryMatchPrefixSegment(path, "/v/"u8, out pathId);
        return hasPathId
               && IsSafeId(pathId)
               && Succeed(VideoProviderKind.YouTubeVideo, pathId, default, listId, default, startSeconds, out match);
    }

    /// <summary>Parses query-based YouTube forms (<c>/watch?v=...</c> and <c>/playlist?list=...</c>).</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="query">URL query slice.</param>
    /// <param name="listId">Validated optional playlist ID.</param>
    /// <param name="startSeconds">Parsed start offset in seconds, or <c>-1</c>.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a query-based YouTube target was matched.</returns>
    private static bool TryParseYouTubeQuery(
        ReadOnlySpan<byte> path,
        ReadOnlySpan<byte> query,
        ReadOnlySpan<byte> listId,
        int startSeconds,
        out VideoMatch match)
    {
        match = default;
        return (EqualsTrimmedPath(path, "/watch"u8)
                && TryGetQueryParam(query, "v"u8, out var videoId)
                && IsSafeId(videoId)
                && Succeed(VideoProviderKind.YouTubeVideo, videoId, default, listId, default, startSeconds, out match))
               || (EqualsTrimmedPath(path, "/playlist"u8)
                   && !listId.IsEmpty
                   && Succeed(VideoProviderKind.YouTubePlaylist, listId, default, default, default, -1, out match));
    }

    /// <summary>Parses Vimeo standard, unlisted, channel, group, showcase, and player URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Vimeo video was matched.</returns>
    private static bool TryParseVimeo(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        match = default;
        return lowerHost.SequenceEqual("player.vimeo.com"u8)
            ? TryMatchPrefixSegment(parts.Path, VideoPathPrefix, out var playerVid)
              && IsAllAsciiDigits(playerVid)
              && Succeed(VideoProviderKind.Vimeo, playerVid, default, parts.Query, parts.Fragment, -1, out match)
            : TryParseVimeoMainHost(parts.Path, parts.Query, parts.Fragment, out match);
    }

    /// <summary>Parses <c>vimeo.com</c> paths including unlisted hashes and collection routes.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="query">URL query slice.</param>
    /// <param name="fragment">URL fragment slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Vimeo video path was matched.</returns>
    private static bool TryParseVimeoMainHost(
        ReadOnlySpan<byte> path,
        ReadOnlySpan<byte> query,
        ReadOnlySpan<byte> fragment,
        out VideoMatch match)
    {
        match = default;
        var trimmed = TrimSlashes(path);
        if (trimmed.IsEmpty)
        {
            return false;
        }

        var firstSlash = trimmed.IndexOf((byte)'/');
        if (firstSlash < 0)
        {
            return IsAllAsciiDigits(trimmed)
                   && Succeed(VideoProviderKind.Vimeo, trimmed, default, query, fragment, -1, out match);
        }

        var first = trimmed[..firstSlash];
        var rest = trimmed[(firstSlash + 1)..];
        if (IsAllAsciiDigits(first))
        {
            return rest.IndexOf((byte)'/') < 0
                   && IsSafeId(rest)
                   && Succeed(VideoProviderKind.Vimeo, first, rest, query, fragment, -1, out match);
        }

        // Handle Vimeo collection routes such as channels, groups, showcase, and album video paths.
        var lastSlash = trimmed.LastIndexOf((byte)'/');
        var tailId = trimmed[(lastSlash + 1)..];
        return IsAllAsciiDigits(tailId)
               && IsVimeoCollectionPrefix(trimmed)
               && Succeed(VideoProviderKind.Vimeo, tailId, default, query, fragment, -1, out match);
    }

    /// <summary>Returns <c>true</c> when <paramref name="trimmedPath"/> starts with a recognized Vimeo collection prefix.</summary>
    /// <param name="trimmedPath">Slash-trimmed Vimeo path.</param>
    /// <returns><c>true</c> on match.</returns>
    private static bool IsVimeoCollectionPrefix(ReadOnlySpan<byte> trimmedPath) =>
        AsciiByteHelpers.StartsWithIgnoreAsciiCase(trimmedPath, 0, "channels/"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(trimmedPath, 0, "groups/"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(trimmedPath, 0, "showcase/"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(trimmedPath, 0, "album/"u8);

    /// <summary>Parses OneDrive personal video share and embed URLs requiring <c>resid=</c> or <c>id=</c>.</summary>
    /// <param name="query">URL query slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a OneDrive resource parameter is present.</returns>
    private static bool TryParseOneDrive(ReadOnlySpan<byte> query, out VideoMatch match)
    {
        match = default;
        return !query.IsEmpty
               && (TryGetQueryParam(query, "resid"u8, out _) || TryGetQueryParam(query, "id"u8, out _))
               && Succeed(VideoProviderKind.OneDrive, default, default, query, default, -1, out match);
    }

    /// <summary>Parses Microsoft Stream and Stream on SharePoint / OneDrive for Business video URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Stream or SharePoint video URL was matched.</returns>
    private static bool TryParseSharePointOrStream(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        match = default;
        if (lowerHost.SequenceEqual("web.microsoftstream.com"u8))
        {
            return (TryMatchPrefixSegment(parts.Path, VideoPathPrefix, out var streamId)
                    || TryMatchPrefixSegment(parts.Path, EmbedVideoPathPrefix, out streamId))
                   && IsSafeId(streamId)
                   && Succeed(VideoProviderKind.MicrosoftStreamClassic, streamId, default, parts.Query, default, -1, out match);
        }

        var layoutOffset = IndexOfIgnoreAsciiCase(parts.Url, "/_layouts/15/embed.aspx"u8);
        if (layoutOffset < 0)
        {
            layoutOffset = IndexOfIgnoreAsciiCase(parts.Url, "/_layouts/15/stream.aspx"u8);
        }

        if (layoutOffset > 0 && !parts.Query.IsEmpty)
        {
            return Succeed(VideoProviderKind.SharePointEmbed, parts.Url[..layoutOffset], default, parts.Query, default, -1, out match);
        }

        if (!AsciiByteHelpers.StartsWithIgnoreAsciiCase(parts.Path, 0, "/:v:/"u8))
        {
            return false;
        }

        var queryOrHashOffset = parts.Url.IndexOfAny((byte)'?', (byte)'#');
        var baseUrl = queryOrHashOffset < 0 ? parts.Url : parts.Url[..queryOrHashOffset];
        return Succeed(VideoProviderKind.SharePointShareLink, baseUrl, default, parts.Query, default, -1, out match);
    }

    /// <summary>Parses Azure AI Video Indexer, Azure Media Player, and Azure Blob / CDN / Front Door video URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when an Azure video endpoint was matched.</returns>
    private static bool TryParseAzure(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        match = default;
        if (lowerHost.SequenceEqual("www.videoindexer.ai"u8)
            || lowerHost.SequenceEqual("videoindexer.ai"u8)
            || lowerHost.SequenceEqual("vi.azure.com"u8))
        {
            return TryParseAzureVideoIndexer(parts.Path, parts.Query, out match);
        }

        if (lowerHost.SequenceEqual("amp.azure.net"u8))
        {
            var hashOffset = parts.Url.IndexOf((byte)'#');
            var cleanUrl = hashOffset < 0 ? parts.Url : parts.Url[..hashOffset];
            return AsciiByteHelpers.StartsWithIgnoreAsciiCase(parts.Path, 0, "/libs/amp/"u8)
                   && VideoUrlProbe.EndsWithIgnoreAsciiCase(parts.Path, "/player.html"u8)
                   && TryGetQueryParam(parts.Query, "url"u8, out _)
                   && Succeed(VideoProviderKind.AzureMediaPlayer, cleanUrl, default, default, default, -1, out match);
        }

        return VideoUrlProbe.TryGetHtml5MimeType(parts.Path, out var mimeType)
               && Succeed(VideoProviderKind.Html5Video, parts.Url, mimeType, default, default, -1, out match);
    }

    /// <summary>Parses Azure AI Video Indexer portal and player widget paths.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="query">URL query slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when an account ID and video ID were extracted.</returns>
    private static bool TryParseAzureVideoIndexer(
        ReadOnlySpan<byte> path,
        ReadOnlySpan<byte> query,
        out VideoMatch match)
    {
        match = default;
        var trimmed = TrimSlashes(path);
        if (AsciiByteHelpers.StartsWithIgnoreAsciiCase(trimmed, 0, "embed/player/"u8))
        {
            var rest = trimmed["embed/player/"u8.Length..];
            return TrySplitTwoSegments(rest, out var accountId, out var videoId)
                   && IsSafeId(accountId)
                   && IsSafeId(videoId)
                   && Succeed(VideoProviderKind.AzureVideoIndexer, videoId, accountId, query, default, -1, out match);
        }

        if (!AsciiByteHelpers.StartsWithIgnoreAsciiCase(trimmed, 0, "accounts/"u8))
        {
            return false;
        }

        var afterAccounts = trimmed["accounts/"u8.Length..];
        var videosOffset = IndexOfIgnoreAsciiCase(afterAccounts, VideosPathPrefix);
        if (videosOffset <= 0)
        {
            return false;
        }

        var acct = afterAccounts[..videosOffset];
        var vid = afterAccounts[(videosOffset + VideosPathPrefix.Length)..];
        var slashOffset = vid.IndexOf((byte)'/');
        if (slashOffset >= 0)
        {
            vid = vid[..slashOffset];
        }

        return IsSafeId(acct)
               && IsSafeId(vid)
               && Succeed(VideoProviderKind.AzureVideoIndexer, vid, acct, query, default, -1, out match);
    }

    /// <summary>Parses Loom <c>/share/{id}</c> and <c>/embed/{id}</c> URLs.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="query">URL query slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Loom video ID was matched.</returns>
    private static bool TryParseLoom(ReadOnlySpan<byte> path, ReadOnlySpan<byte> query, out VideoMatch match)
    {
        match = default;
        return (TryMatchPrefixSegment(path, "/share/"u8, out var id)
                || TryMatchPrefixSegment(path, EmbedPathPrefix, out id))
               && IsSafeId(id)
               && Succeed(VideoProviderKind.Loom, id, default, query, default, -1, out match);
    }

    /// <summary>Parses Dailymotion <c>/video/{id}</c>, <c>dai.ly/{id}</c>, and <c>geo.dailymotion.com/player</c> URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Dailymotion video was matched.</returns>
    private static bool TryParseDailymotion(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        match = default;
        if (lowerHost.SequenceEqual("geo.dailymotion.com"u8))
        {
            var hashOffset = parts.Url.IndexOf((byte)'#');
            var clean = hashOffset < 0 ? parts.Url : parts.Url[..hashOffset];
            return AsciiByteHelpers.StartsWithIgnoreAsciiCase(parts.Path, 0, "/player"u8)
                   && TryGetQueryParam(parts.Query, "video"u8, out var geoVid)
                   && IsSafeId(geoVid)
                   && Succeed(VideoProviderKind.DailymotionPlayer, clean, default, default, default, -1, out match);
        }

        if (!TryExtractDailymotionRawId(parts.Path, lowerHost, out var rawId))
        {
            return false;
        }

        var underscoreOffset = rawId.IndexOf((byte)'_');
        var videoId = underscoreOffset > 0 ? rawId[..underscoreOffset] : rawId;
        return IsSafeId(videoId)
               && Succeed(VideoProviderKind.Dailymotion, videoId, default, parts.Query, default, -1, out match);
    }

    /// <summary>Extracts the raw Dailymotion path segment before slug stripping.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="rawId">Extracted segment on success.</param>
    /// <returns><c>true</c> when a candidate segment was found.</returns>
    private static bool TryExtractDailymotionRawId(
        ReadOnlySpan<byte> path,
        scoped ReadOnlySpan<byte> lowerHost,
        out ReadOnlySpan<byte> rawId) =>
        lowerHost.SequenceEqual("dai.ly"u8)
            ? TryReadSingleSegment(path, out rawId)
            : TryMatchPrefixSegment(path, VideoPathPrefix, out rawId)
              || TryMatchPrefixSegment(path, EmbedVideoPathPrefix, out rawId);

    /// <summary>Parses Twitch VOD, Clip, and player URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Twitch VOD, Clip, or player URL was matched.</returns>
    private static bool TryParseTwitch(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        match = default;
        if (lowerHost.SequenceEqual("clips.twitch.tv"u8))
        {
            return TryParseTwitchClipsHost(parts.Path, parts.Query, out match);
        }

        return lowerHost.SequenceEqual("player.twitch.tv"u8)
            ? (TryGetQueryParam(parts.Query, "video"u8, out _)
               || TryGetQueryParam(parts.Query, "channel"u8, out _))
              && Succeed(VideoProviderKind.TwitchPlayer, parts.Query, default, default, default, -1, out match)
            : TryParseTwitchMainHost(parts.Path, out match);
    }

    /// <summary>Parses <c>clips.twitch.tv</c> slug and <c>/embed?clip=</c> URLs.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="query">URL query slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a valid clip slug was matched.</returns>
    private static bool TryParseTwitchClipsHost(
        ReadOnlySpan<byte> path,
        ReadOnlySpan<byte> query,
        out VideoMatch match)
    {
        match = default;
        return EqualsTrimmedPath(path, "/embed"u8)
            ? TryGetQueryParam(query, "clip"u8, out var clipParam)
              && IsSafeId(clipParam)
              && Succeed(VideoProviderKind.TwitchClip, clipParam, default, default, default, -1, out match)
            : TryReadSingleSegment(path, out var clipSlug)
              && IsSafeId(clipSlug)
              && Succeed(VideoProviderKind.TwitchClip, clipSlug, default, default, default, -1, out match);
    }

    /// <summary>Parses <c>twitch.tv/videos/{id}</c> and <c>twitch.tv/{channel}/clip/{slug}</c> URLs.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a VOD or channel clip path was matched.</returns>
    private static bool TryParseTwitchMainHost(ReadOnlySpan<byte> path, out VideoMatch match)
    {
        match = default;
        if (TryMatchPrefixSegment(path, VideosPathPrefix, out var vodId) && IsAllAsciiDigits(vodId))
        {
            return Succeed(VideoProviderKind.TwitchVideo, vodId, default, default, default, -1, out match);
        }

        var clipOffset = IndexOfIgnoreAsciiCase(path, "/clip/"u8);
        if (clipOffset <= 0)
        {
            return false;
        }

        var afterClip = TrimSlashes(path[(clipOffset + "/clip/"u8.Length)..]);
        return afterClip.IndexOf((byte)'/') < 0
               && IsSafeId(afterClip)
               && Succeed(VideoProviderKind.TwitchClip, afterClip, default, default, default, -1, out match);
    }

    /// <summary>Parses Wistia <c>/medias/{id}</c> and <c>/embed/iframe/{id}</c> URLs.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="query">URL query slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Wistia media ID was matched.</returns>
    private static bool TryParseWistia(
        ReadOnlySpan<byte> path,
        ReadOnlySpan<byte> query,
        out VideoMatch match)
    {
        match = default;
        return (TryMatchPrefixSegment(path, "/medias/"u8, out var id)
                || TryMatchPrefixSegment(path, "/embed/iframe/"u8, out id))
               && IsSafeId(id)
               && Succeed(VideoProviderKind.Wistia, id, default, query, default, -1, out match);
    }

    /// <summary>Parses Cloudflare Stream customer-subdomain, <c>cloudflarestream.com</c>, and <c>videodelivery.net</c> URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Cloudflare Stream video UID was matched.</returns>
    private static bool TryParseCloudflareStream(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        match = default;
        if (!TryExtractCloudflareUid(parts.Path, out var uid))
        {
            return false;
        }

        if (lowerHost.StartsWith("customer-"u8))
        {
            return Succeed(VideoProviderKind.CloudflareCustomer, uid, parts.Host, parts.Query, default, -1, out match);
        }

        var kind = lowerHost.EndsWith("videodelivery.net"u8)
            ? VideoProviderKind.CloudflareVideoDelivery
            : VideoProviderKind.CloudflareGeneric;
        return Succeed(kind, uid, default, parts.Query, default, -1, out match);
    }

    /// <summary>Extracts and validates the Cloudflare Stream UID from <paramref name="path"/>.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="uid">Extracted UID on success.</param>
    /// <returns><c>true</c> when <paramref name="path"/> is <c>/{uid}</c>, <c>/{uid}/iframe</c>, or <c>/{uid}/watch</c>.</returns>
    private static bool TryExtractCloudflareUid(ReadOnlySpan<byte> path, out ReadOnlySpan<byte> uid)
    {
        uid = default;
        var trimmed = TrimSlashes(path);
        if (trimmed.IsEmpty)
        {
            return false;
        }

        var slashOffset = trimmed.IndexOf((byte)'/');
        var candidate = slashOffset < 0 ? trimmed : trimmed[..slashOffset];
        if (!IsSafeId(candidate))
        {
            return false;
        }

        if (slashOffset >= 0)
        {
            var tail = trimmed[(slashOffset + 1)..];
            if (!AsciiByteHelpers.EqualsIgnoreAsciiCase(tail, "iframe"u8)
                && !AsciiByteHelpers.EqualsIgnoreAsciiCase(tail, "watch"u8))
            {
                return false;
            }
        }

        uid = candidate;
        return true;
    }

    /// <summary>Parses Google Drive <c>/file/d/{id}/view</c> and <c>/file/d/{id}/preview</c> URLs.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Google Drive file ID was matched.</returns>
    private static bool TryParseGoogleDrive(ReadOnlySpan<byte> path, out VideoMatch match)
    {
        match = default;
        if (!AsciiByteHelpers.StartsWithIgnoreAsciiCase(path, 0, "/file/d/"u8))
        {
            return false;
        }

        var rest = TrimSlashes(path["/file/d/"u8.Length..]);
        var slashOffset = rest.IndexOf((byte)'/');
        var fileId = slashOffset < 0 ? rest : rest[..slashOffset];
        return IsSafeId(fileId)
               && Succeed(VideoProviderKind.GoogleDrive, fileId, default, default, default, -1, out match);
    }

    /// <summary>Parses Bilibili <c>/video/BV...</c>, <c>/video/av...</c>, and <c>player.bilibili.com/player.html</c> URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Bilibili video was matched.</returns>
    private static bool TryParseBilibili(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        match = default;
        return lowerHost.SequenceEqual("player.bilibili.com"u8)
            ? EqualsTrimmedPath(parts.Path, "/player.html"u8)
              && (TryGetQueryParam(parts.Query, "bvid"u8, out _) || TryGetQueryParam(parts.Query, "aid"u8, out _))
              && Succeed(VideoProviderKind.BilibiliPlayer, parts.Query, default, default, default, -1, out match)
            : TryMatchPrefixSegment(parts.Path, VideoPathPrefix, out var rawId)
              && TryParseBilibiliVideoId(rawId, parts.Query, out match);
    }

    /// <summary>Parses a <c>BV...</c> or <c>av...</c> Bilibili identifier and optional <c>p=</c> part index.</summary>
    /// <param name="rawId">Segment after <c>/video/</c>.</param>
    /// <param name="query">URL query slice.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when <paramref name="rawId"/> is a valid <c>BV</c> or <c>av</c> identifier.</returns>
    private static bool TryParseBilibiliVideoId(
        ReadOnlySpan<byte> rawId,
        ReadOnlySpan<byte> query,
        out VideoMatch match)
    {
        match = default;
        if (rawId.Length <= BilibiliPrefixLength)
        {
            return false;
        }

        _ = TryGetQueryParam(query, "p"u8, out var pageParam);
        if (!pageParam.IsEmpty && !IsAllAsciiDigits(pageParam))
        {
            pageParam = default;
        }

        if (AsciiByteHelpers.StartsWithIgnoreAsciiCase(rawId, 0, "bv"u8))
        {
            return IsSafeId(rawId)
                   && Succeed(VideoProviderKind.BilibiliBv, rawId, default, pageParam, default, -1, out match);
        }

        if (!AsciiByteHelpers.StartsWithIgnoreAsciiCase(rawId, 0, "av"u8))
        {
            return false;
        }

        var digits = rawId[BilibiliPrefixLength..];
        return IsAllAsciiDigits(digits)
               && Succeed(VideoProviderKind.BilibiliAv, digits, default, pageParam, default, -1, out match);
    }

    /// <summary>Parses Vidyard <c>share.vidyard.com/watch/{uuid}</c> and <c>play.vidyard.com/{uuid}(.html)</c> URLs.</summary>
    /// <param name="parts">Decomposed URL slices.</param>
    /// <param name="lowerHost">Lowercased host bytes.</param>
    /// <param name="match">Populated match on success.</param>
    /// <returns><c>true</c> when a Vidyard player UUID was matched.</returns>
    private static bool TryParseVidyard(
        ParsedUrl parts,
        scoped ReadOnlySpan<byte> lowerHost,
        out VideoMatch match)
    {
        match = default;
        var extracted = lowerHost.SequenceEqual("share.vidyard.com"u8)
            ? TryMatchPrefixSegment(parts.Path, "/watch/"u8, out var rawId)
            : TryReadSingleSegment(parts.Path, out rawId);
        if (!extracted)
        {
            return false;
        }

        if (VideoUrlProbe.EndsWithIgnoreAsciiCase(rawId, ".html"u8))
        {
            rawId = rawId[..^".html"u8.Length];
        }

        return IsSafeId(rawId)
               && Succeed(VideoProviderKind.Vidyard, rawId, default, parts.Query, default, -1, out match);
    }

    /// <summary>Constructs <paramref name="match"/> and returns <c>true</c>.</summary>
    /// <param name="kind">Matched concrete provider kind.</param>
    /// <param name="primary">Primary slice.</param>
    /// <param name="secondary">Secondary slice.</param>
    /// <param name="query">Query slice.</param>
    /// <param name="fragment">Fragment slice.</param>
    /// <param name="startSeconds">Start offset in seconds, or <c>-1</c>.</param>
    /// <param name="match">Output match.</param>
    /// <returns>Always <c>true</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Succeed(
        VideoProviderKind kind,
        ReadOnlySpan<byte> primary,
        ReadOnlySpan<byte> secondary,
        ReadOnlySpan<byte> query,
        ReadOnlySpan<byte> fragment,
        int startSeconds,
        out VideoMatch match)
    {
        match = new(kind, primary, secondary, query, fragment, startSeconds);
        return true;
    }

    /// <summary>Parses YouTube <c>start=</c> or <c>t=</c> query parameters (plain seconds or <c>1h2m3s</c> notation).</summary>
    /// <param name="query">URL query slice.</param>
    /// <returns>Start offset in positive whole seconds, or <c>-1</c> when absent or invalid.</returns>
    private static int ParseYouTubeStartSeconds(ReadOnlySpan<byte> query)
    {
        if (query.IsEmpty)
        {
            return -1;
        }

        if (TryGetQueryParam(query, "start"u8, out var startVal) && TryParsePositiveSeconds(startVal, out var sec))
        {
            return sec;
        }

        return TryGetQueryParam(query, "t"u8, out var tVal) && TryParseDurationSeconds(tVal, out var dur) ? dur : -1;
    }

    /// <summary>Parses a plain positive integer seconds count.</summary>
    /// <param name="span">Candidate ASCII digits.</param>
    /// <param name="seconds">Parsed seconds on success.</param>
    /// <returns><c>true</c> when <paramref name="span"/> is a positive integer.</returns>
    private static bool TryParsePositiveSeconds(ReadOnlySpan<byte> span, out int seconds)
    {
        if (Utf8Parser.TryParse(span, out int parsed, out var consumed) && consumed == span.Length && parsed > 0)
        {
            seconds = parsed;
            return true;
        }

        seconds = -1;
        return false;
    }

    /// <summary>Parses either plain seconds (<c>90</c>) or <c>1h2m3s</c> YouTube timestamp notation.</summary>
    /// <param name="span">Candidate duration parameter bytes.</param>
    /// <param name="seconds">Total positive seconds on success.</param>
    /// <returns><c>true</c> when <paramref name="span"/> represents a positive duration.</returns>
    private static bool TryParseDurationSeconds(ReadOnlySpan<byte> span, out int seconds)
    {
        if (TryParsePositiveSeconds(span, out seconds))
        {
            return true;
        }

        seconds = -1;
        var total = 0;
        var current = 0;
        var hasDigits = false;
        for (var i = 0; i < span.Length; i++)
        {
            var b = span[i];
            if (AsciiByteHelpers.IsAsciiDigit(b))
            {
                const int Radix = 10;
                current = (current * Radix) + (b - (byte)'0');
                hasDigits = true;
                continue;
            }

            var factor = DurationUnitSeconds(b);
            if (!hasDigits || factor is 0)
            {
                return false;
            }

            total += current * factor;
            current = 0;
            hasDigits = false;
        }

        total += hasDigits ? current : 0;
        if (total <= 0)
        {
            return false;
        }

        seconds = total;
        return true;
    }

    /// <summary>Maps an ASCII unit character (<c>h</c>, <c>m</c>, <c>s</c>) to seconds, or <c>0</c> when unrecognized.</summary>
    /// <param name="unit">Candidate unit byte.</param>
    /// <returns>Seconds per unit, or <c>0</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DurationUnitSeconds(byte unit) =>
        AsciiByteHelpers.ToAsciiLowerByte(unit) switch
        {
            (byte)'h' => SecondsPerHour,
            (byte)'m' => SecondsPerMinute,
            (byte)'s' => 1,
            _ => 0,
        };

    /// <summary>Splits <c>first/second</c> (ignoring any trailing slash) into two non-empty segments.</summary>
    /// <param name="input">Path tail to split.</param>
    /// <param name="first">First path segment.</param>
    /// <param name="second">Second path segment.</param>
    /// <returns><c>true</c> when two non-empty segments were extracted.</returns>
    private static bool TrySplitTwoSegments(
        ReadOnlySpan<byte> input,
        out ReadOnlySpan<byte> first,
        out ReadOnlySpan<byte> second)
    {
        first = default;
        second = default;
        var trimmed = TrimSlashes(input);
        var slashOffset = trimmed.IndexOf((byte)'/');
        if (slashOffset <= 0 || slashOffset + 1 >= trimmed.Length)
        {
            return false;
        }

        first = trimmed[..slashOffset];
        var rest = trimmed[(slashOffset + 1)..];
        var nextSlash = rest.IndexOf((byte)'/');
        second = nextSlash < 0 ? rest : rest[..nextSlash];
        return !second.IsEmpty;
    }

    /// <summary>Reads the single path segment after <paramref name="lowerPrefix"/> (such as <c>/shorts/</c>).</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="lowerPrefix">Lowercase prefix ending in <c>/</c>.</param>
    /// <param name="segment">Extracted segment on success.</param>
    /// <returns><c>true</c> when a non-empty segment follows <paramref name="lowerPrefix"/>.</returns>
    private static bool TryMatchPrefixSegment(
        ReadOnlySpan<byte> path,
        ReadOnlySpan<byte> lowerPrefix,
        out ReadOnlySpan<byte> segment)
    {
        segment = default;
        if (!AsciiByteHelpers.StartsWithIgnoreAsciiCase(path, 0, lowerPrefix))
        {
            return false;
        }

        var rest = TrimSlashes(path[lowerPrefix.Length..]);
        if (rest.IsEmpty)
        {
            return false;
        }

        var slashOffset = rest.IndexOf((byte)'/');
        segment = slashOffset < 0 ? rest : rest[..slashOffset];
        return true;
    }

    /// <summary>Reads a single top-level segment from <paramref name="path"/> (failing when nested segments exist).</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="segment">Extracted single segment.</param>
    /// <returns><c>true</c> when <paramref name="path"/> contains exactly one non-empty segment.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadSingleSegment(ReadOnlySpan<byte> path, out ReadOnlySpan<byte> segment)
    {
        segment = TrimSlashes(path);
        return !segment.IsEmpty && segment.IndexOf((byte)'/') < 0;
    }

    /// <summary>Checks whether <paramref name="path"/> (ignoring a single trailing slash) equals <paramref name="lowerExpected"/>.</summary>
    /// <param name="path">URL path slice.</param>
    /// <param name="lowerExpected">Lowercase expected path.</param>
    /// <returns><c>true</c> on case-insensitive equality.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualsTrimmedPath(ReadOnlySpan<byte> path, ReadOnlySpan<byte> lowerExpected)
    {
        var normalized = path.Length > 1 && path[^1] is (byte)'/' ? path[..^1] : path;
        return AsciiByteHelpers.EqualsIgnoreAsciiCase(normalized, lowerExpected);
    }

    /// <summary>Trims leading and trailing <c>/</c> bytes.</summary>
    /// <param name="value">Span to trim.</param>
    /// <returns>Trimmed slice.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<byte> TrimSlashes(ReadOnlySpan<byte> value) =>
        value.Trim((byte)'/');

    /// <summary>Case-insensitive ASCII substring search over <paramref name="haystack"/>.</summary>
    /// <param name="haystack">UTF-8 bytes to search.</param>
    /// <param name="lowerNeedle">Lowercase ASCII needle.</param>
    /// <returns>Zero-based byte offset of the first match, or <c>-1</c>.</returns>
    private static int IndexOfIgnoreAsciiCase(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> lowerNeedle)
    {
        for (var i = 0; i + lowerNeedle.Length <= haystack.Length; i++)
        {
            if (AsciiByteHelpers.StartsWithIgnoreAsciiCase(haystack, i, lowerNeedle))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Decomposed slices of an HTTP(S) URL inside the caller's source buffer.</summary>
    [System.Diagnostics.DebuggerDisplay("ParsedUrl: HostLen={Host.Length}, PathLen={Path.Length}")]
    private readonly ref struct ParsedUrl
    {
        /// <summary>Initializes a new instance of the <see cref="ParsedUrl"/> struct.</summary>
        /// <param name="url">Full input URL slice.</param>
        /// <param name="host">Host slice without port.</param>
        /// <param name="path">Path slice starting with <c>/</c>, or empty.</param>
        /// <param name="query">Query slice without leading <c>?</c>.</param>
        /// <param name="fragment">Fragment slice without leading <c>#</c>.</param>
        internal ParsedUrl(
            ReadOnlySpan<byte> url,
            ReadOnlySpan<byte> host,
            ReadOnlySpan<byte> path,
            ReadOnlySpan<byte> query,
            ReadOnlySpan<byte> fragment)
        {
            Url = url;
            Host = host;
            Path = path;
            Query = query;
            Fragment = fragment;
        }

        /// <summary>Gets the full input URL slice.</summary>
        internal ReadOnlySpan<byte> Url { get; }

        /// <summary>Gets the host slice without port.</summary>
        internal ReadOnlySpan<byte> Host { get; }

        /// <summary>Gets the path slice starting at <c>/</c>, or empty.</summary>
        internal ReadOnlySpan<byte> Path { get; }

        /// <summary>Gets the query slice without leading <c>?</c>.</summary>
        internal ReadOnlySpan<byte> Query { get; }

        /// <summary>Gets the fragment slice without leading <c>#</c>.</summary>
        internal ReadOnlySpan<byte> Fragment { get; }
    }
}
