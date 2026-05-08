// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Layouts;

/// <summary>Builder-extension surface for the layouts plugin.</summary>
public static class DocBuilderLayoutsExtensions
{
    /// <summary>Registers <see cref="LayoutsPlugin"/> with the default option set.</summary>
    /// <param name="builder">Builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseLayouts(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new LayoutsPlugin());
    }

    /// <summary>Registers <see cref="LayoutsPlugin"/> with options-customization.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="configure">Function that receives <see cref="LayoutsOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseLayouts(this DocBuilder builder, Func<LayoutsOptions, LayoutsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(LayoutsOptions.Default);
        return builder.UsePlugin(new LayoutsPlugin(options));
    }

    /// <summary>Registers <see cref="LayoutsPlugin"/> with options-customization and a logger.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="configure">Options customization.</param>
    /// <param name="logger">Logger that receives diagnostic warnings.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseLayouts(this DocBuilder builder, Func<LayoutsOptions, LayoutsOptions> configure, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(logger);
        var options = configure(LayoutsOptions.Default);
        return builder.UsePlugin(new LayoutsPlugin(options, logger));
    }

    /// <summary>Registers <see cref="LayoutsPlugin"/> with pre-built options and a logger.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="options">Resolved options.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseLayouts(this DocBuilder builder, LayoutsOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new LayoutsPlugin(options, logger));
    }

    /// <summary>Registers <see cref="LayoutsPlugin"/> with pre-built options and no logger.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="options">Resolved options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseLayouts(this DocBuilder builder, LayoutsOptions options) => builder.UseLayouts(options, NullLogger.Instance);
}
