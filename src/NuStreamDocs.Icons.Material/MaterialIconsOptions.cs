// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Icons.Material;

/// <summary>Configuration for <see cref="MaterialIconsPlugin"/>.</summary>
/// <param name="Style">Which Material icon family to load.</param>
/// <param name="StylesheetUrlOverride">Optional explicit UTF-8 stylesheet URL; empty to derive from <see cref="Style"/>.</param>
/// <param name="Preconnect">When true, also emit <c>&lt;link rel="preconnect"&gt;</c> hints to fonts.googleapis.com / fonts.gstatic.com.</param>
public readonly record struct MaterialIconsOptions(
    MaterialIconStyle Style,
    byte[] StylesheetUrlOverride,
    bool Preconnect)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static MaterialIconsOptions Default { get; } = new(
        Style: MaterialIconStyle.SymbolsOutlined,
        StylesheetUrlOverride: [],
        Preconnect: true);

    /// <summary>Resolves the stylesheet URL bytes according to <see cref="Style"/> (or the override).</summary>
    /// <returns>UTF-8 stylesheet URL bytes ready for direct emission into a <c>&lt;link href&gt;</c>.</returns>
    public byte[] ResolveStylesheetUrl() =>
        StylesheetUrlOverride.Length > 0
            ? StylesheetUrlOverride
            : Style switch
            {
                MaterialIconStyle.Classic => [.. "https://fonts.googleapis.com/icon?family=Material+Icons"u8],
                MaterialIconStyle.SymbolsOutlined => [.. "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined"u8],
                MaterialIconStyle.SymbolsRounded => [.. "https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded"u8],
                MaterialIconStyle.SymbolsSharp => [.. "https://fonts.googleapis.com/css2?family=Material+Symbols+Sharp"u8],
                _ => [.. "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined"u8],
            };
}
