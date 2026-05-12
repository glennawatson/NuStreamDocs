// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.ContentLoader;

/// <summary>Builder extensions that register a <see cref="ContentLoaderPlugin"/>.</summary>
public static class DocBuilderContentLoaderExtensions
{
    /// <summary>Registers a content-loader plugin that runs the supplied loaders during the discover phase.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="loaders">The loaders to run.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseContentLoaders(this DocBuilder builder, params IContentLoader[] loaders) =>
        builder.UsePlugin(new ContentLoaderPlugin(loaders));

    /// <summary>Registers a content-loader plugin with a logger.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="logger">Logger that receives loader diagnostics.</param>
    /// <param name="loaders">The loaders to run.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseContentLoaders(this DocBuilder builder, ILogger logger, params IContentLoader[] loaders) =>
        builder.UsePlugin(new ContentLoaderPlugin(loaders, logger));

    /// <summary>Registers a single content loader.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="loader">The loader to run.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseContentLoader(this DocBuilder builder, IContentLoader loader) =>
        builder.UsePlugin(new ContentLoaderPlugin([loader]));
}
