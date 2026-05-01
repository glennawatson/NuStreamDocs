// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Search;

/// <summary>Builder-extension surface for <see cref="SearchPlugin"/>.</summary>
public static class DocBuilderSearchExtensions
{
    /// <summary>Registers <see cref="SearchPlugin"/> with default options (Pagefind).</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSearch(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new SearchPlugin());
    }

    /// <summary>Registers <see cref="SearchPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="SearchOptions.Default"/> and returns the customised set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSearch(this DocBuilder builder, Func<SearchOptions, SearchOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(SearchOptions.Default);
        return builder.UsePlugin(new SearchPlugin(options));
    }

    /// <summary>Registers <see cref="SearchPlugin"/> with caller-supplied options and a logger.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSearch(this DocBuilder builder, in SearchOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new SearchPlugin(options, logger));
    }
}
