// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Metadata;

/// <summary>Builder extension that registers <see cref="MetadataPlugin"/>.</summary>
public static class DocBuilderMetadataExtensions
{
    /// <summary>Registers <see cref="MetadataPlugin"/> with default options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMetadata(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new MetadataPlugin());
    }

    /// <summary>Registers <see cref="MetadataPlugin"/> with the supplied options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMetadata(this DocBuilder builder, MetadataOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new MetadataPlugin(options));
    }
}
