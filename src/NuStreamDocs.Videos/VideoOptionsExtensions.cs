// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Videos;

/// <summary>UTF-8 fluent configuration helpers for <see cref="VideoOptions"/>.</summary>
public static class VideoOptionsExtensions
{
    /// <summary>Extension members for <see cref="VideoOptions"/>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in VideoOptions options)
    {
        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="providers"/> as the enabled provider set.</summary>
        /// <param name="providers">Enabled video providers.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithProviders(VideoProviders providers) =>
            options with { Providers = providers };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling standalone bare-URL embedding.</summary>
        /// <param name="enabled"><c>true</c> to embed standalone bare URLs; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithEmbedBareUrls(bool enabled) =>
            options with { EmbedBareUrls = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling <c>![alt](url)</c> media embedding.</summary>
        /// <param name="enabled"><c>true</c> to embed image-syntax video links; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithEmbedImageLinks(bool enabled) =>
            options with { EmbedImageLinks = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling standalone <c>[title](url)</c> embedding.</summary>
        /// <param name="enabled"><c>true</c> to embed standalone markdown links; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithEmbedStandaloneLinks(bool enabled) =>
            options with { EmbedStandaloneLinks = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling <c>youtube-nocookie.com</c> embeds.</summary>
        /// <param name="enabled"><c>true</c> to use <c>www.youtube-nocookie.com</c>; <c>false</c> to use <c>www.youtube.com</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithYouTubePrivacyEnhanced(bool enabled) =>
            options with { YouTubePrivacyEnhanced = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling Vimeo's <c>dnt=1</c> query parameter.</summary>
        /// <param name="enabled"><c>true</c> to append <c>dnt=1</c>; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithVimeoDoNotTrack(bool enabled) =>
            options with { VimeoDoNotTrack = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling <c>loading="lazy"</c> on iframes.</summary>
        /// <param name="enabled"><c>true</c> to emit <c>loading="lazy"</c>; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithLazyLoad(bool enabled) =>
            options with { LazyLoad = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> with <paramref name="enabled"/> controlling bundled CSS emission.</summary>
        /// <param name="enabled"><c>true</c> to emit and link <c>video.css</c>; otherwise, <c>false</c>.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithIncludeStylesheet(bool enabled) =>
            options with { IncludeStylesheet = enabled };

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="containerClass"/> as the wrapper CSS class.</summary>
        /// <param name="containerClass">UTF-8 CSS class bytes.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithContainerClass(byte[] containerClass)
        {
            ArgumentNullException.ThrowIfNull(containerClass);
            return options with { ContainerClass = containerClass };
        }

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="containerClass"/> as the wrapper CSS class.</summary>
        /// <param name="containerClass">UTF-8 CSS class span (for example <c>"video-wrap"u8</c>).</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithContainerClass(ReadOnlySpan<byte> containerClass) =>
            options with { ContainerClass = containerClass.ToArray() };

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="twitchParent"/> as Twitch's <c>parent</c> domain.</summary>
        /// <param name="twitchParent">UTF-8 host name bytes without scheme or path.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithTwitchParent(byte[] twitchParent)
        {
            ArgumentNullException.ThrowIfNull(twitchParent);
            return options with { TwitchParent = twitchParent };
        }

        /// <summary>Returns a copy of <paramref name="options"/> using <paramref name="twitchParent"/> as Twitch's <c>parent</c> domain.</summary>
        /// <param name="twitchParent">UTF-8 host name span (for example <c>"docs.example.com"u8</c>).</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VideoOptions WithTwitchParent(ReadOnlySpan<byte> twitchParent) =>
            options with { TwitchParent = twitchParent.ToArray() };
    }
}
