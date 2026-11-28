// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Images;

/// <summary>Builder-extension surface for <see cref="ImagesPlugin"/>.</summary>
public static class DocBuilderImagesExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="ImagesPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseImages() => builder.UsePlugin(new ImagesPlugin());

        /// <summary>Registers <see cref="ImagesPlugin"/> with caller-customized options.</summary>
        /// <param name="configure">Function receiving <see cref="ImagesOptions.Default"/> and returning customized options.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseImages(Func<ImagesOptions, ImagesOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new ImagesPlugin(configure(ImagesOptions.Default)));
        }

        /// <summary>Registers <see cref="ImagesPlugin"/> with caller-supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseImages(in ImagesOptions options) =>
            builder.UsePlugin(new ImagesPlugin(options));
    }
}
