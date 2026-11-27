// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Videos;

/// <summary>Builder-extension surface for <see cref="VideoPlugin"/>.</summary>
public static class DocBuilderVideoExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="VideoPlugin"/> with <see cref="VideoOptions.Default"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseVideos() =>
            builder.UsePlugin(new VideoPlugin());

        /// <summary>Registers <see cref="VideoPlugin"/> with the supplied options.</summary>
        /// <param name="options">Video embed options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseVideos(in VideoOptions options) =>
            builder.UsePlugin(new VideoPlugin(options));

        /// <summary>Registers <see cref="VideoPlugin"/> with options produced from <see cref="VideoOptions.Default"/>.</summary>
        /// <param name="configure">Callback receiving <see cref="VideoOptions.Default"/> and returning customized options.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseVideos(Func<VideoOptions, VideoOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new VideoPlugin(configure(VideoOptions.Default)));
        }
    }
}
