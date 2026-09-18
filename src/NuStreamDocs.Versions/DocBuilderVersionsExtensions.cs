// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Versions;

/// <summary>Builder extension that registers the versions plugin.</summary>
public static class DocBuilderVersionsExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="VersionsPlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseVersions(VersionOptions options) =>
            builder.UsePlugin(new VersionsPlugin(options));

        /// <summary>Registers <see cref="VersionsPlugin"/> with options and a logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger for diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseVersions(VersionOptions options, ILogger logger) =>
            builder.UsePlugin(new VersionsPlugin(options, logger));
    }
}
