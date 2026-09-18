// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Feed;

/// <summary>Builder extension that registers <see cref="FeedPlugin"/>.</summary>
public static class DocBuilderFeedExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder receiving the feed plugin.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="FeedPlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseFeed(FeedOptions options) =>
            builder.UsePlugin(new FeedPlugin(options));

        /// <summary>Registers <see cref="FeedPlugin"/> with options and a logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseFeed(FeedOptions options, ILogger logger) =>
            builder.UsePlugin(new FeedPlugin(options, TimeProvider.System, logger));
    }
}
