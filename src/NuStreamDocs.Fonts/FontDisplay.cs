// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts;

/// <summary>Maps to the CSS <c>font-display</c> descriptor for the generated <c>@font-face</c> rules.</summary>
public enum FontDisplay
{
    /// <summary>Browser default behavior (<c>auto</c>).</summary>
    Auto,

    /// <summary>Short block period, infinite swap period (<c>block</c>).</summary>
    Block,

    /// <summary>Tiny block period, infinite swap period (<c>swap</c>) — the usual choice for body text.</summary>
    Swap,

    /// <summary>Tiny block period, short swap period (<c>fallback</c>).</summary>
    Fallback,

    /// <summary>Tiny block period, no swap period (<c>optional</c>) — the font is used only if it's already cached.</summary>
    Optional,
}
