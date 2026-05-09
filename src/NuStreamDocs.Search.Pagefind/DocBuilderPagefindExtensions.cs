// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Builder-extension surface for <see cref="PagefindSearchPlugin"/>.</summary>
public static class DocBuilderPagefindExtensions
{
    /// <summary>Registers <see cref="PagefindSearchPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UsePagefindSearch(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new PagefindSearchPlugin());
    }

    /// <summary>Registers <see cref="PagefindSearchPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="PagefindOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UsePagefindSearch(this DocBuilder builder, Func<PagefindOptions, PagefindOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(PagefindOptions.Default);
        return builder.UsePlugin(new PagefindSearchPlugin(options));
    }

    /// <summary>Registers <see cref="PagefindSearchPlugin"/> with caller-supplied options and a logger.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UsePagefindSearch(this DocBuilder builder, in PagefindOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new PagefindSearchPlugin(options, logger));
    }
}
