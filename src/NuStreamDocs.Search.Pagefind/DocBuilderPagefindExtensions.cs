// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Builder-extension surface for <see cref="PagefindSearchPlugin"/>.</summary>
public static class DocBuilderPagefindExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder receiving the search plugin.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="PagefindSearchPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UsePagefindSearch() =>
            builder.UsePlugin(new PagefindSearchPlugin());

        /// <summary>Registers <see cref="PagefindSearchPlugin"/> with caller-tweaked options.</summary>
        /// <param name="configure">Function that receives <see cref="PagefindOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UsePagefindSearch(
            Func<PagefindOptions, PagefindOptions> configure)
        {
            var options = configure(PagefindOptions.Default);
            return builder.UsePlugin(new PagefindSearchPlugin(options));
        }

        /// <summary>Registers <see cref="PagefindSearchPlugin"/> with caller-supplied options and a logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UsePagefindSearch(in PagefindOptions options, ILogger logger) =>
            builder.UsePlugin(new PagefindSearchPlugin(options, logger));
    }
}
