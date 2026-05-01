// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Theme.Common;
using NuStreamDocs.Theme.Material3.IconShortcode;

namespace NuStreamDocs.Theme.Material3;

/// <summary>Builder-extension surface for the Material 3 theme.</summary>
public static class DocBuilderMaterial3Extensions
{
    /// <summary>Registers <see cref="Material3ThemePlugin"/> with default options + the Material3 icon-shortcode preprocessor.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterial3Theme(this DocBuilder builder) =>
        UseMaterial3Theme(builder, iconResolver: null);

    /// <summary>Registers <see cref="Material3ThemePlugin"/> with default options + the Material3 icon-shortcode preprocessor wired to <paramref name="iconResolver"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="iconResolver">
    /// Optional inline-icon resolver (e.g.
    /// <c>NuStreamDocs.Icons.MaterialDesign.MdiIconResolver</c>)
    /// consulted for <c>:material-foo:</c> shortcodes before the
    /// font-ligature fallback.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterial3Theme(this DocBuilder builder, IIconResolver? iconResolver)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .UsePlugin(new IconShortcodePlugin(iconResolver))
            .UsePlugin(new Material3ThemePlugin());
    }

    /// <summary>Registers <see cref="Material3ThemePlugin"/> with caller-tweaked options + the Material3 icon-shortcode preprocessor.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="Material3ThemeOptions.Default"/> and returns the customised set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterial3Theme(this DocBuilder builder, Func<Material3ThemeOptions, Material3ThemeOptions> configure) =>
        UseMaterial3Theme(builder, configure, iconResolver: null);

    /// <summary>Registers <see cref="Material3ThemePlugin"/> with caller-tweaked options + the Material3 icon-shortcode preprocessor wired to <paramref name="iconResolver"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="Material3ThemeOptions.Default"/> and returns the customised set.</param>
    /// <param name="iconResolver">Optional inline-icon resolver consulted for <c>:material-foo:</c> shortcodes before the font-ligature fallback.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterial3Theme(this DocBuilder builder, Func<Material3ThemeOptions, Material3ThemeOptions> configure, IIconResolver? iconResolver)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(Material3ThemeOptions.Default);
        return builder
            .UsePlugin(new IconShortcodePlugin(iconResolver))
            .UsePlugin(new Material3ThemePlugin(options));
    }
}
