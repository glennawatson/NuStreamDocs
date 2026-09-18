// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Toc;

/// <summary>Builder-extension surface for <see cref="TocPlugin"/>.</summary>
public static class DocBuilderTocExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="TocPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseToc() => builder.UsePlugin(new TocPlugin());

        /// <summary>Registers <see cref="TocPlugin"/> with caller-supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseToc(in TocOptions options) =>
            builder.UsePlugin(new TocPlugin(options));

        /// <summary>Registers <see cref="TocPlugin"/> with caller-supplied options and a logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger forwarded to the plugin.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseToc(in TocOptions options, ILogger logger) =>
            builder.UsePlugin(new TocPlugin(options, logger));
    }
}
