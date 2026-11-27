// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Videos;

/// <summary>Selects which video hosting platforms <see cref="VideoPlugin"/> recognizes and embeds.</summary>
[Flags]
public enum VideoProviders
{
    /// <summary>Disables all video providers.</summary>
    None = 0,

    /// <summary>YouTube videos, Shorts, live streams, and playlists (<c>youtube.com</c>, <c>youtu.be</c>, <c>youtube-nocookie.com</c>).</summary>
    YouTube = 1 << 0,

    /// <summary>Vimeo standard, unlisted, channel, group, and showcase videos (<c>vimeo.com</c>, <c>player.vimeo.com</c>).</summary>
    Vimeo = 1 << 1,

    /// <summary>Microsoft OneDrive personal shared and embedded videos (<c>onedrive.live.com</c>).</summary>
    OneDrive = 1 << 2,

    /// <summary>Microsoft Stream and Stream on SharePoint / OneDrive for Business videos (<c>*.sharepoint.com</c>, <c>web.microsoftstream.com</c>).</summary>
    SharePointStream = 1 << 3,

    /// <summary>Azure AI Video Indexer, Azure Media Player, and Azure Blob / CDN / Front Door video streams.</summary>
    Azure = 1 << 4,

    /// <summary>Loom shared and embedded recordings (<c>loom.com</c>).</summary>
    Loom = 1 << 5,

    /// <summary>Dailymotion videos (<c>dailymotion.com</c>, <c>dai.ly</c>, <c>geo.dailymotion.com</c>).</summary>
    Dailymotion = 1 << 6,

    /// <summary>Twitch VODs and Clips (<c>twitch.tv</c>, <c>clips.twitch.tv</c>, <c>player.twitch.tv</c>).</summary>
    Twitch = 1 << 7,

    /// <summary>Wistia hosted videos (<c>*.wistia.com</c>, <c>fast.wistia.net</c>).</summary>
    Wistia = 1 << 8,

    /// <summary>Cloudflare Stream videos (<c>*.cloudflarestream.com</c>, <c>videodelivery.net</c>).</summary>
    CloudflareStream = 1 << 9,

    /// <summary>Google Drive shared video previews (<c>drive.google.com</c>).</summary>
    GoogleDrive = 1 << 10,

    /// <summary>Bilibili BV and av videos (<c>bilibili.com</c>, <c>player.bilibili.com</c>).</summary>
    Bilibili = 1 << 11,

    /// <summary>Vidyard shared and embedded videos (<c>share.vidyard.com</c>, <c>play.vidyard.com</c>).</summary>
    Vidyard = 1 << 12,

    /// <summary>Direct HTTPS/HTTP HTML5 video files (<c>.mp4</c>, <c>.webm</c>, <c>.ogv</c>, <c>.m4v</c>, <c>.mov</c>).</summary>
    Html5File = 1 << 13,

    /// <summary>Enables every built-in video provider and direct HTML5 video files.</summary>
    All = YouTube
          | Vimeo
          | OneDrive
          | SharePointStream
          | Azure
          | Loom
          | Dailymotion
          | Twitch
          | Wistia
          | CloudflareStream
          | GoogleDrive
          | Bilibili
          | Vidyard
          | Html5File,
}
