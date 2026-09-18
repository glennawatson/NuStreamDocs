// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Lightbox;

/// <summary>Builder extension that registers <see cref="LightboxPlugin"/>.</summary>
public static class DocBuilderLightboxExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers the lightbox plugin with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLightbox() => builder.UsePlugin(new LightboxPlugin());

        /// <summary>Registers the lightbox plugin with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLightbox(LightboxOptions options) =>
            builder.UsePlugin(new LightboxPlugin(options));
    }
}
