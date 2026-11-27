// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Videos;

/// <summary>Concrete embed shape resolved by <see cref="VideoUrlParser"/>.</summary>
internal enum VideoProviderKind
{
    /// <summary>No recognized video target.</summary>
    None = 0,

    /// <summary>Standard 16:9 YouTube video or live stream.</summary>
    YouTubeVideo = 1,

    /// <summary>Vertical 9:16 YouTube Short.</summary>
    YouTubeShorts = 2,

    /// <summary>YouTube playlist (<c>videoseries?list=...</c>).</summary>
    YouTubePlaylist = 3,

    /// <summary>Vimeo player embed.</summary>
    Vimeo = 4,

    /// <summary>OneDrive personal embed (<c>onedrive.live.com/embed?...</c>).</summary>
    OneDrive = 5,

    /// <summary>Classic Microsoft Stream video (<c>web.microsoftstream.com/embed/video/...</c>).</summary>
    MicrosoftStreamClassic = 6,

    /// <summary>SharePoint / Stream on SharePoint layout embed (<c>_layouts/15/embed.aspx?...</c>).</summary>
    SharePointEmbed = 7,

    /// <summary>SharePoint <c>:v:</c> shared video link with <c>action=embedview</c>.</summary>
    SharePointShareLink = 8,

    /// <summary>Azure AI Video Indexer player widget.</summary>
    AzureVideoIndexer = 9,

    /// <summary>Azure Media Player hosted iframe.</summary>
    AzureMediaPlayer = 10,

    /// <summary>Loom video embed.</summary>
    Loom = 11,

    /// <summary>Dailymotion video ID rewritten to the geo player.</summary>
    Dailymotion = 12,

    /// <summary>Pre-formed Dailymotion geo player URL.</summary>
    DailymotionPlayer = 13,

    /// <summary>Twitch VOD (<c>player.twitch.tv/?video=...</c>).</summary>
    TwitchVideo = 14,

    /// <summary>Twitch Clip (<c>clips.twitch.tv/embed?clip=...</c>).</summary>
    TwitchClip = 15,

    /// <summary>Pre-formed Twitch player URL requiring <c>parent=</c> completion when absent.</summary>
    TwitchPlayer = 16,

    /// <summary>Wistia iframe embed (<c>fast.wistia.net/embed/iframe/...</c>).</summary>
    Wistia = 17,

    /// <summary>Cloudflare Stream customer-subdomain embed (<c>customer-X.cloudflarestream.com/UID/iframe</c>).</summary>
    CloudflareCustomer = 18,

    /// <summary>Cloudflare Stream generic <c>iframe.cloudflarestream.com</c> embed.</summary>
    CloudflareGeneric = 19,

    /// <summary>Cloudflare Stream <c>iframe.videodelivery.net</c> embed.</summary>
    CloudflareVideoDelivery = 20,

    /// <summary>Google Drive file preview (<c>drive.google.com/file/d/ID/preview</c>).</summary>
    GoogleDrive = 21,

    /// <summary>Bilibili <c>BV</c> video (<c>player.bilibili.com/player.html?bvid=...</c>).</summary>
    BilibiliBv = 22,

    /// <summary>Bilibili numeric <c>av</c> video (<c>player.bilibili.com/player.html?aid=...</c>).</summary>
    BilibiliAv = 23,

    /// <summary>Pre-formed Bilibili player URL.</summary>
    BilibiliPlayer = 24,

    /// <summary>Vidyard player (<c>play.vidyard.com/UUID.html</c>).</summary>
    Vidyard = 25,

    /// <summary>Direct HTML5 <c>&lt;video&gt;</c> element (MP4, WebM, Ogg, M4V, MOV, including Azure Blob/CDN/Front Door).</summary>
    Html5Video = 26,
}
