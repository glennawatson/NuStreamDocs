// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Transitions;

/// <summary>Configuration for <c>TransitionsPlugin</c>.</summary>
/// <param name="ContentSelector">UTF-8 CSS selector for the page region the router swaps on navigation (the article body).</param>
/// <param name="NavSelector">UTF-8 CSS selector for an additional region to swap (the sidebar / nav markup); empty leaves the chrome untouched.</param>
/// <param name="Animation">The transition played on swap, where the View Transitions API is available.</param>
/// <param name="Prefetch">When the router pre-fetches link targets.</param>
/// <param name="PrefetchDelayMs">Debounce, in milliseconds, before a hover-triggered pre-fetch fires.</param>
/// <param name="IgnoreSelector">UTF-8 CSS selector for links the router must not intercept or pre-fetch (downloads, new-tab links, opt-outs).</param>
/// <param name="Enabled">Master switch; when false the plugin contributes nothing.</param>
public readonly record struct TransitionsOptions(
    byte[] ContentSelector,
    byte[] NavSelector,
    TransitionAnimation Animation,
    PrefetchStrategy Prefetch,
    int PrefetchDelayMs,
    byte[] IgnoreSelector,
    bool Enabled)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static TransitionsOptions Default { get; } = new(
        ContentSelector: [.. "[data-md-component='content']"u8],
        NavSelector: [],
        Animation: TransitionAnimation.Fade,
        Prefetch: PrefetchStrategy.Hover,
        PrefetchDelayMs: 80,
        IgnoreSelector: [.. "[download],[target='_blank'],[data-no-router],[rel~='external']"u8],
        Enabled: true);
}
