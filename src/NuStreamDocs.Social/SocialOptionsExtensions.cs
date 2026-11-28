// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Social;

/// <summary>Fluent helpers for building <see cref="SocialOptions"/>.</summary>
public static class SocialOptionsExtensions
{
    /// <summary>Extension members for <see cref="SocialOptions"/>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in SocialOptions options)
    {
        /// <summary>Sets the <c>twitter:site</c> handle (for example <c>"@nustreamdocs"u8</c>).</summary>
        /// <param name="handle">UTF-8 account handle bytes.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SocialOptions WithTwitterSite(ReadOnlySpan<byte> handle) =>
            options with { TwitterSite = handle.ToArray() };

        /// <summary>Sets the background and accent colors used when rendering generated SVG social cards.</summary>
        /// <param name="backgroundColor">UTF-8 CSS fill color for the card canvas.</param>
        /// <param name="accentColor">UTF-8 CSS fill color for the accent bars.</param>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SocialOptions WithCardColors(ReadOnlySpan<byte> backgroundColor, ReadOnlySpan<byte> accentColor) =>
            options with { BackgroundColor = backgroundColor.ToArray(), AccentColor = accentColor.ToArray() };

        /// <summary>Turns off build-time SVG social card generation.</summary>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SocialOptions WithoutSvgCards() =>
            options with { GenerateSvgCards = false };

        /// <summary>Turns off Schema.org JSON-LD emission.</summary>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SocialOptions WithoutJsonLd() =>
            options with { EmitJsonLd = false };

        /// <summary>Turns off Twitter/X card meta tag emission.</summary>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SocialOptions WithoutTwitterCard() =>
            options with { EmitTwitterCard = false };

        /// <summary>Turns off OpenGraph meta tag emission.</summary>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SocialOptions WithoutOpenGraph() =>
            options with { EmitOpenGraph = false };

        /// <summary>Turns off <c>&lt;link rel="canonical"&gt;</c> tag emission.</summary>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SocialOptions WithoutCanonicalLink() =>
            options with { EmitCanonicalLink = false };

        /// <summary>Disables the social plugin entirely.</summary>
        /// <returns>The updated options.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SocialOptions Disable() =>
            options with { Enabled = false };
    }
}
