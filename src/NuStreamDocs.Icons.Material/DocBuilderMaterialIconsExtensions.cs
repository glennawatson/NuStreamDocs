// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Icons.Material;

/// <summary>Builder-extension surface for <see cref="MaterialIconsPlugin"/>.</summary>
public static class DocBuilderMaterialIconsExtensions
{
    /// <summary>Registers <see cref="MaterialIconsPlugin"/> with default options (Material Symbols Outlined).</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterialIcons(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new MaterialIconsPlugin());
    }

    /// <summary>Registers <see cref="MaterialIconsPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="MaterialIconsOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMaterialIcons(this DocBuilder builder, Func<MaterialIconsOptions, MaterialIconsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(MaterialIconsOptions.Default);
        return builder.UsePlugin(new MaterialIconsPlugin(options));
    }
}
