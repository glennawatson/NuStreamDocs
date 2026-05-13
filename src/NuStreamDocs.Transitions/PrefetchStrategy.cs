// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Transitions;

/// <summary>When the client router pre-fetches a same-origin page so the eventual navigation is instant.</summary>
public enum PrefetchStrategy
{
    /// <summary>Never pre-fetch; each navigation triggers its own fetch.</summary>
    Off,

    /// <summary>Pre-fetch a link's target on <c>mouseenter</c> / <c>touchstart</c> (after a short debounce).</summary>
    Hover,

    /// <summary>Pre-fetch every same-origin link as it scrolls into view.</summary>
    Viewport
}
