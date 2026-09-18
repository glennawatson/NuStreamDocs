// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Icons.Material;

/// <summary>Builder-extension surface for <see cref="MaterialIconsPlugin"/>.</summary>
public static class DocBuilderMaterialIconsExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="MaterialIconsPlugin"/> with default options (Material Symbols Outlined).</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMaterialIcons() => builder.UsePlugin(new MaterialIconsPlugin());

        /// <summary>Registers <see cref="MaterialIconsPlugin"/> with caller-tweaked options.</summary>
        /// <param name="configure">Function that receives <see cref="MaterialIconsOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseMaterialIcons(
            Func<MaterialIconsOptions, MaterialIconsOptions> configure)
        {
            var options = configure(MaterialIconsOptions.Default);
            return builder.UsePlugin(new MaterialIconsPlugin(options));
        }
    }
}
