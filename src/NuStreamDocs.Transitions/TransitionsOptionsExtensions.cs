// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Transitions;

/// <summary>Fluent helpers for building <see cref="TransitionsOptions"/>.</summary>
public static class TransitionsOptionsExtensions
{
    /// <summary>Extension members for <c>TransitionsOptions</c>.</summary>
    /// <param name="options">Options to update.</param>
    extension(in TransitionsOptions options)
    {
        /// <summary>Replaces the swapped content-region selector.</summary>
        /// <param name="selector">UTF-8 CSS selector for the article body region.</param>
        /// <returns>The updated options.</returns>
        public TransitionsOptions WithContentSelector(
            ReadOnlySpan<byte> selector) =>
            options with { ContentSelector = selector.ToArray() };

        /// <summary>Replaces the additional swapped nav-region selector (empty leaves the chrome untouched).</summary>
        /// <param name="selector">UTF-8 CSS selector for the sidebar / nav region.</param>
        /// <returns>The updated options.</returns>
        public TransitionsOptions WithNavSelector(ReadOnlySpan<byte> selector) =>
            options with { NavSelector = selector.ToArray() };

        /// <summary>Replaces the swap animation.</summary>
        /// <param name="animation">The transition to play on swap.</param>
        /// <returns>The updated options.</returns>
        public TransitionsOptions WithAnimation(TransitionAnimation animation) =>
            options with { Animation = animation };

        /// <summary>Replaces the pre-fetch strategy.</summary>
        /// <param name="prefetch">When the router pre-fetches link targets.</param>
        /// <returns>The updated options.</returns>
        public TransitionsOptions WithPrefetch(PrefetchStrategy prefetch) =>
            options with { Prefetch = prefetch };

        /// <summary>Disables pre-fetching.</summary>
        /// <returns>The updated options.</returns>
        public TransitionsOptions WithoutPrefetch() =>
            options with { Prefetch = PrefetchStrategy.Off };

        /// <summary>Replaces the hover-pre-fetch debounce, in milliseconds.</summary>
        /// <param name="delayMs">Debounce in milliseconds.</param>
        /// <returns>The updated options.</returns>
        public TransitionsOptions WithPrefetchDelay(int delayMs) =>
            options with { PrefetchDelayMs = delayMs };

        /// <summary>Replaces the selector for links the router must ignore.</summary>
        /// <param name="selector">UTF-8 CSS selector matched against candidate links.</param>
        /// <returns>The updated options.</returns>
        public TransitionsOptions
            WithIgnoreSelector(ReadOnlySpan<byte> selector) =>
            options with { IgnoreSelector = selector.ToArray() };

        /// <summary>Disables the plugin (it then contributes nothing).</summary>
        /// <returns>The updated options.</returns>
        public TransitionsOptions Disable() =>
            options with { Enabled = false };
    }
}
