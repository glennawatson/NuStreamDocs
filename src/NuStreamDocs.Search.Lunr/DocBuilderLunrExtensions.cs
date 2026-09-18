// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Search.Lunr;

/// <summary>Builder-extension surface for <see cref="LunrSearchPlugin"/>.</summary>
public static class DocBuilderLunrExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">The builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="LunrSearchPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLunrSearch() => builder.UsePlugin(new LunrSearchPlugin());

        /// <summary>Registers <see cref="LunrSearchPlugin"/> with caller-tweaked options.</summary>
        /// <param name="configure">Function that receives <see cref="LunrOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseLunrSearch(Func<LunrOptions, LunrOptions> configure)
        {
            var options = configure(LunrOptions.Default);
            return builder.UsePlugin(new LunrSearchPlugin(options));
        }

        /// <summary>Registers <see cref="LunrSearchPlugin"/> with caller-supplied options and a logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLunrSearch(in LunrOptions options, ILogger logger) =>
            builder.UsePlugin(new LunrSearchPlugin(options, logger));
    }
}
