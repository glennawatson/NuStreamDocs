// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Macros;

/// <summary>
/// Builder-extension surface for the macros plugin.
/// </summary>
/// <remarks>
/// <code>
/// builder.UseMacros(opts =&gt; opts with
/// {
///     Variables = { ["site"] = "NuStreamDocs", ["version"] = "1.0" },
/// });
/// </code>
/// </remarks>
public static class DocBuilderMacrosExtensions
{
    /// <summary>Registers <see cref="MacrosPlugin"/> with the default option set.</summary>
    /// <param name="builder">Builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMacros(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new MacrosPlugin());
    }

    /// <summary>Registers <see cref="MacrosPlugin"/> with options-customisation.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="configure">Function that receives <see cref="MacrosOptions.Default"/> and returns the customised set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMacros(this DocBuilder builder, Func<MacrosOptions, MacrosOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(MacrosOptions.Default);
        return builder.UsePlugin(new MacrosPlugin(options));
    }

    /// <summary>Registers <see cref="MacrosPlugin"/> with options + logger.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="configure">Options customisation.</param>
    /// <param name="logger">Logger that receives missing-variable warnings.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMacros(this DocBuilder builder, Func<MacrosOptions, MacrosOptions> configure, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(logger);
        var options = configure(MacrosOptions.Default);
        return builder.UsePlugin(new MacrosPlugin(options, logger));
    }

    /// <summary>Convenience overload — pre-built options + logger.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="options">Resolved options.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMacros(this DocBuilder builder, MacrosOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new MacrosPlugin(options, logger));
    }

    /// <summary>Convenience overload — pre-built options, no logger.</summary>
    /// <param name="builder">Builder.</param>
    /// <param name="options">Resolved options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMacros(this DocBuilder builder, MacrosOptions options) =>
        UseMacros(builder, options, NullLogger.Instance);
}
