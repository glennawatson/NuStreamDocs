// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Videos;

/// <summary>Configuration for <see cref="VideoPlugin"/>.</summary>
/// <param name="Providers">Enabled video platforms.</param>
/// <param name="EmbedBareUrls">When <c>true</c>, standalone bare video URLs and <c>&lt;https://...&gt;</c> autolink lines expand into video players.</param>
/// <param name="EmbedImageLinks">When <c>true</c>, Markdown media links (<c>![title](url)</c>) targeting a recognized video URL expand into video players.</param>
/// <param name="EmbedStandaloneLinks">When <c>true</c>, standalone <c>[title](url)</c> lines expand into video players; <c>false</c> by default so link lists stay links.</param>
/// <param name="YouTubePrivacyEnhanced">When <c>true</c>, YouTube embeds target <c>www.youtube-nocookie.com</c> instead of <c>www.youtube.com</c>.</param>
/// <param name="VimeoDoNotTrack">When <c>true</c>, Vimeo embeds append <c>dnt=1</c> when no <c>dnt</c> parameter is already present.</param>
/// <param name="LazyLoad">When <c>true</c>, emitted <c>&lt;iframe&gt;</c> elements carry <c>loading="lazy"</c>.</param>
/// <param name="IncludeStylesheet">When <c>true</c>, emits the responsive <c>video.css</c> asset and links it in <c>&lt;head&gt;</c>.</param>
/// <param name="ContainerClass">UTF-8 CSS class name applied to the outer responsive wrapper <c>&lt;div&gt;</c> (default <c>nd-video</c>).</param>
/// <param name="TwitchParent">UTF-8 host name passed as Twitch's required <c>parent</c> query parameter; empty falls back to the configured site URL host or <c>localhost</c>.</param>
[System.Diagnostics.DebuggerDisplay("VideoOptions: {Providers}")]
public readonly record struct VideoOptions(
    VideoProviders Providers,
    bool EmbedBareUrls,
    bool EmbedImageLinks,
    bool EmbedStandaloneLinks,
    bool YouTubePrivacyEnhanced,
    bool VimeoDoNotTrack,
    bool LazyLoad,
    bool IncludeStylesheet,
    byte[] ContainerClass,
    byte[] TwitchParent)
{
    /// <summary>Gets the default outer wrapper CSS class (<c>nd-video</c>).</summary>
    public static byte[] DefaultContainerClass { get; } = [.. "nd-video"u8];

    /// <summary>Gets the default options: all providers enabled, bare URLs and <c>![alt](url)</c> enabled, privacy-enhanced YouTube, Vimeo DNT, and lazy loading.</summary>
    public static VideoOptions Default { get; } = new(
        VideoProviders.All,
        true,
        true,
        false,
        true,
        true,
        true,
        true,
        DefaultContainerClass,
        []);
}
