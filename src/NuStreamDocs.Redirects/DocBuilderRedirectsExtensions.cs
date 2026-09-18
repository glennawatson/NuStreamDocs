// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Redirects;

/// <summary>Builder-extension surface for <see cref="RedirectsPlugin"/>.</summary>
public static class DocBuilderRedirectsExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="RedirectsPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseRedirects() => builder.UsePlugin(new RedirectsPlugin());

        /// <summary>Registers <see cref="RedirectsPlugin"/> with caller-tweaked options.</summary>
        /// <param name="configure">Function that receives <see cref="RedirectsOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseRedirects(Func<RedirectsOptions, RedirectsOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new RedirectsPlugin(configure(RedirectsOptions.Default)));
        }

        /// <summary>Registers <see cref="RedirectsPlugin"/> with caller-supplied options and a logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseRedirects(in RedirectsOptions options, ILogger logger) =>
            builder.UsePlugin(new RedirectsPlugin(options, logger));
    }
}
