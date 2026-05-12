// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Transitions;

/// <summary>The transition played when the content region is swapped (only when the browser supports the View Transitions API and the user hasn't requested reduced motion).</summary>
public enum TransitionAnimation
{
    /// <summary>A short cross-fade of the page root.</summary>
    Fade,

    /// <summary>No animation — the swap is instant even where the View Transitions API is available.</summary>
    None,
}
