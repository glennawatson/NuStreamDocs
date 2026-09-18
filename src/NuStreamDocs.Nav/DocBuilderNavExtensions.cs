// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Nav;

/// <summary>Builder-extension surface for <see cref="NavPlugin"/>.</summary>
public static class DocBuilderNavExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="NavPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseNav() => builder.UsePlugin(new NavPlugin());

        /// <summary>Registers <see cref="NavPlugin"/> with caller-tweaked options.</summary>
        /// <param name="configure">Function that receives <see cref="NavOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseNav(Func<NavOptions, NavOptions> configure)
        {
            var options = configure(NavOptions.Default);
            return builder.UsePlugin(new NavPlugin(options));
        }

        /// <summary>Registers <see cref="NavPlugin"/> with caller-tweaked options and a logger.</summary>
        /// <param name="configure">Function that receives <see cref="NavOptions.Default"/> and returns the customized set.</param>
        /// <param name="logger">Logger forwarded to the plugin.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseNav(Func<NavOptions, NavOptions> configure, ILogger logger)
        {
            var options = configure(NavOptions.Default);
            return builder.UsePlugin(new NavPlugin(options, logger));
        }
    }
}
