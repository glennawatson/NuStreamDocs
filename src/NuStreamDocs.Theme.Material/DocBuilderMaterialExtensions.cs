// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Theme.Common;
using NuStreamDocs.Theme.Material.IconShortcode;

namespace NuStreamDocs.Theme.Material;

/// <summary>Builder-extension surface for the Material theme.</summary>
public static class DocBuilderMaterialExtensions
{
    /// <summary>Registers <see cref="MaterialThemePlugin"/> with default options + the Material icon-shortcode preprocessor.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterialTheme(this DocBuilder builder) => builder.UseMaterialTheme(iconResolver: null);

    /// <summary>Registers <see cref="MaterialThemePlugin"/> with default options + the Material icon-shortcode preprocessor wired to <paramref name="iconResolver"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="iconResolver">
    /// Optional inline-icon resolver (e.g.
    /// <c>NuStreamDocs.Icons.MaterialDesign.MdiIconResolver</c>)
    /// consulted for <c>:material-foo:</c> shortcodes before the
    /// font-ligature fallback.
    /// </param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterialTheme(this DocBuilder builder, IIconResolver? iconResolver)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .UsePlugin(new IconShortcodePlugin(iconResolver))
            .UsePlugin(new MaterialThemePlugin());
    }

    /// <summary>Registers <see cref="MaterialThemePlugin"/> with caller-tweaked options + the Material icon-shortcode preprocessor.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="MaterialThemeOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterialTheme(this DocBuilder builder, Func<MaterialThemeOptions, MaterialThemeOptions> configure) => builder.UseMaterialTheme(configure, iconResolver: null);

    /// <summary>Registers <see cref="MaterialThemePlugin"/> with caller-tweaked options + the Material icon-shortcode preprocessor wired to <paramref name="iconResolver"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="MaterialThemeOptions.Default"/> and returns the customized set.</param>
    /// <param name="iconResolver">Optional inline-icon resolver consulted for <c>:material-foo:</c> shortcodes before the font-ligature fallback.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterialTheme(this DocBuilder builder, Func<MaterialThemeOptions, MaterialThemeOptions> configure, IIconResolver? iconResolver)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(MaterialThemeOptions.Default);
        return builder
            .UsePlugin(new IconShortcodePlugin(iconResolver))
            .UsePlugin(new MaterialThemePlugin(options));
    }
}
