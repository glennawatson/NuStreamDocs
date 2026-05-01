// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Nav;

/// <summary>
/// Builder-extension surface for <see cref="NavPlugin"/>.
/// </summary>
/// <remarks>
/// Each plugin assembly ships its own <c>DocBuilder*Extensions</c>
/// static class so consumers register with a single readable line:
/// <code>
/// new DocBuilder()
///     .UseNav(opts =&gt; opts with { HideEmptySections = false })
///     .Build();
/// </code>
/// The generic <c>UsePlugin&lt;TPlugin&gt;()</c> on
/// <see cref="DocBuilder"/> remains the AOT seam; these helpers just
/// capture options before the plugin instance is constructed.
/// </remarks>
public static class DocBuilderNavExtensions
{
    /// <summary>Registers <see cref="NavPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseNav(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new NavPlugin());
    }

    /// <summary>Registers <see cref="NavPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="NavOptions.Default"/> and returns the customised set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseNav(this DocBuilder builder, Func<NavOptions, NavOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        var options = configure(NavOptions.Default);
        return builder.UsePlugin(new NavPlugin(options));
    }

    /// <summary>Registers <see cref="NavPlugin"/> with caller-tweaked options and a logger.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="NavOptions.Default"/> and returns the customised set.</param>
    /// <param name="logger">Logger forwarded to the plugin.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseNav(this DocBuilder builder, Func<NavOptions, NavOptions> configure, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(logger);
        var options = configure(NavOptions.Default);
        return builder.UsePlugin(new NavPlugin(options, logger));
    }
}
