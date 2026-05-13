// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Transitions;

/// <summary>Fluent helpers for building <see cref="TransitionsOptions"/>.</summary>
public static class TransitionsOptionsExtensions
{
    /// <summary>Replaces the swapped content-region selector.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="selector">UTF-8 CSS selector for the article body region.</param>
    /// <returns>The updated options.</returns>
    public static TransitionsOptions WithContentSelector(
        this in TransitionsOptions options,
        ReadOnlySpan<byte> selector) =>
        options with { ContentSelector = selector.ToArray() };

    /// <summary>Replaces the additional swapped nav-region selector (empty leaves the chrome untouched).</summary>
    /// <param name="options">Source options.</param>
    /// <param name="selector">UTF-8 CSS selector for the sidebar / nav region.</param>
    /// <returns>The updated options.</returns>
    public static TransitionsOptions WithNavSelector(this in TransitionsOptions options, ReadOnlySpan<byte> selector) =>
        options with { NavSelector = selector.ToArray() };

    /// <summary>Replaces the swap animation.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="animation">The transition to play on swap.</param>
    /// <returns>The updated options.</returns>
    public static TransitionsOptions WithAnimation(this in TransitionsOptions options, TransitionAnimation animation) =>
        options with { Animation = animation };

    /// <summary>Replaces the pre-fetch strategy.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="prefetch">When the router pre-fetches link targets.</param>
    /// <returns>The updated options.</returns>
    public static TransitionsOptions WithPrefetch(this in TransitionsOptions options, PrefetchStrategy prefetch) =>
        options with { Prefetch = prefetch };

    /// <summary>Disables pre-fetching.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static TransitionsOptions WithoutPrefetch(this in TransitionsOptions options) =>
        options with { Prefetch = PrefetchStrategy.Off };

    /// <summary>Replaces the hover-pre-fetch debounce, in milliseconds.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="delayMs">Debounce in milliseconds.</param>
    /// <returns>The updated options.</returns>
    public static TransitionsOptions WithPrefetchDelay(this in TransitionsOptions options, int delayMs) =>
        options with { PrefetchDelayMs = delayMs };

    /// <summary>Replaces the selector for links the router must ignore.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="selector">UTF-8 CSS selector matched against candidate links.</param>
    /// <returns>The updated options.</returns>
    public static TransitionsOptions
        WithIgnoreSelector(this in TransitionsOptions options, ReadOnlySpan<byte> selector) =>
        options with { IgnoreSelector = selector.ToArray() };

    /// <summary>Disables the plugin (it then contributes nothing).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static TransitionsOptions Disable(this in TransitionsOptions options) =>
        options with { Enabled = false };
}
