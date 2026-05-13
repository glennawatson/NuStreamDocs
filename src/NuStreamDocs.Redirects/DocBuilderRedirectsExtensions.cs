// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Redirects;

/// <summary>Builder-extension surface for <see cref="RedirectsPlugin"/>.</summary>
public static class DocBuilderRedirectsExtensions
{
    /// <summary>Registers <see cref="RedirectsPlugin"/> with default options.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseRedirects(this DocBuilder builder) => builder.UsePlugin(new RedirectsPlugin());

    /// <summary>Registers <see cref="RedirectsPlugin"/> with caller-tweaked options.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Function that receives <see cref="RedirectsOptions.Default"/> and returns the customized set.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseRedirects(this DocBuilder builder, Func<RedirectsOptions, RedirectsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return builder.UsePlugin(new RedirectsPlugin(configure(RedirectsOptions.Default)));
    }

    /// <summary>Registers <see cref="RedirectsPlugin"/> with caller-supplied options and a logger.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseRedirects(this DocBuilder builder, in RedirectsOptions options, ILogger logger) =>
        builder.UsePlugin(new RedirectsPlugin(options, logger));
}
