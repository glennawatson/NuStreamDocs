// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Playgrounds;

/// <summary><see cref="DocBuilder"/> registration extensions for <see cref="PlaygroundsPlugin"/> and its fence handlers.</summary>
public static class DocBuilderPlaygroundsExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Site builder.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="PlaygroundsPlugin"/> and its <c>SuperFences</c> handlers with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UsePlaygrounds() => builder.UsePlaygrounds(PlaygroundsOptions.Default);

        /// <summary>Registers <see cref="PlaygroundsPlugin"/> and its <c>SuperFences</c> handlers with <paramref name="options"/>.</summary>
        /// <param name="options">Playground options.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <c>null</c>.</exception>
        public DocBuilder UsePlaygrounds(PlaygroundsOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);
            return builder
                .UsePlugin(new HttpRequestFenceHandler(options))
                .UsePlugin(new PlaygroundFenceHandler())
                .UsePlugin(new PlaygroundsPlugin(options));
        }

        /// <summary>Registers <see cref="PlaygroundsPlugin"/> configured via <paramref name="configure"/>.</summary>
        /// <param name="configure">Options transform.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <c>null</c>.</exception>
        public DocBuilder UsePlaygrounds(Func<PlaygroundsOptions, PlaygroundsOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlaygrounds(configure(PlaygroundsOptions.Default));
        }
    }
}
