// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Metadata;

/// <summary>Builder extension that registers <see cref="MetadataPlugin"/>.</summary>
public static class DocBuilderMetadataExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="MetadataPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMetadata() => builder.UsePlugin(new MetadataPlugin());

        /// <summary>Registers <see cref="MetadataPlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMetadata(MetadataOptions options) =>
            builder.UsePlugin(new MetadataPlugin(options));
    }
}
