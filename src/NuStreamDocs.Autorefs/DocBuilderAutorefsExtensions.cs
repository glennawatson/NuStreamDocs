// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Autorefs;

/// <summary>Builder extension that registers <see cref="AutorefsPlugin"/>.</summary>
public static class DocBuilderAutorefsExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers the autorefs plugin with a fresh registry.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAutorefs() => builder.UsePlugin(new AutorefsPlugin());

        /// <summary>Registers the autorefs plugin with a fresh registry sized for <paramref name="initialCapacity"/> entries.</summary>
        /// <param name="initialCapacity">Expected total ID count.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseAutorefs(int initialCapacity)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
            return builder.UsePlugin(new AutorefsPlugin(new(initialCapacity)));
        }

        /// <summary>Registers the autorefs plugin against a pre-existing shared registry.</summary>
        /// <param name="registry">Shared registry; other plugins may already hold a reference and publish into it.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAutorefs(AutorefsRegistry registry) =>
            builder.UsePlugin(new AutorefsPlugin(registry));

        /// <summary>Registers the autorefs plugin against a shared registry with a logger.</summary>
        /// <param name="registry">Shared registry.</param>
        /// <param name="logger">Logger forwarded to the plugin.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseAutorefs(AutorefsRegistry registry, ILogger logger) =>
            builder.UsePlugin(new AutorefsPlugin(registry, logger));
    }
}
