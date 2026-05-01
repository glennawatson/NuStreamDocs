// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Blog;

/// <summary>Builder extensions that register the Wyam blog plugin.</summary>
public static class DocBuilderBlogExtensions
{
    /// <summary>Registers <see cref="WyamBlogPlugin"/> with the supplied options.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseWyamBlog(this DocBuilder builder, WyamBlogOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new WyamBlogPlugin(options));
    }

    /// <summary>Registers <see cref="WyamBlogPlugin"/> with options and a logger.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseWyamBlog(this DocBuilder builder, WyamBlogOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new WyamBlogPlugin(options, logger));
    }
}
