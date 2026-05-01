// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Icons.Material;

/// <summary>Configuration for <see cref="MaterialIconsPlugin"/>.</summary>
/// <param name="Style">Which Material icon family to load.</param>
/// <param name="StylesheetUrlOverride">Optional explicit stylesheet URL; when empty, the URL is derived from <see cref="Style"/>.</param>
/// <param name="Preconnect">When true, also emit <c>&lt;link rel="preconnect"&gt;</c> hints to fonts.googleapis.com / fonts.gstatic.com.</param>
public readonly record struct MaterialIconsOptions(
    MaterialIconStyle Style,
    string StylesheetUrlOverride,
    bool Preconnect)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static MaterialIconsOptions Default { get; } = new(
        Style: MaterialIconStyle.SymbolsOutlined,
        StylesheetUrlOverride: string.Empty,
        Preconnect: true);

    /// <summary>Resolves the stylesheet URL according to <see cref="Style"/> (or the override).</summary>
    /// <returns>The active stylesheet URL.</returns>
    public string ResolveStylesheetUrl() =>
        !string.IsNullOrEmpty(StylesheetUrlOverride)
            ? StylesheetUrlOverride
            : Style switch
            {
                MaterialIconStyle.Classic => "https://fonts.googleapis.com/icon?family=Material+Icons",
                MaterialIconStyle.SymbolsOutlined => "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined",
                MaterialIconStyle.SymbolsRounded => "https://fonts.googleapis.com/css2?family=Material+Symbols+Rounded",
                MaterialIconStyle.SymbolsSharp => "https://fonts.googleapis.com/css2?family=Material+Symbols+Sharp",
                _ => "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined",
            };
}
