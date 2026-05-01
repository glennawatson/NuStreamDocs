// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Theme.Material.IconShortcode;

namespace NuStreamDocs.Theme.Material;

/// <summary>Builder-extension surface for the Material theme.</summary>
public static class DocBuilderMaterialExtensions
{
    /// <summary>Registers <see cref="MaterialThemePlugin"/> with default options + the Material icon-shortcode preprocessor.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterialTheme(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder
            .UsePlugin(new IconShortcodePlugin())
            .UsePlugin(new MaterialThemePlugin());
    }

    /// <summary>Registers <see cref="MaterialThemePlugin"/> with caller-tweaked options + the Material icon-shortcode preprocessor.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="MaterialThemeOptions.Default"/> and returns the customised set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterialTheme(this DocBuilder builder, Func<MaterialThemeOptions, MaterialThemeOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(MaterialThemeOptions.Default);
        return builder
            .UsePlugin(new IconShortcodePlugin())
            .UsePlugin(new MaterialThemePlugin(options));
    }
}
