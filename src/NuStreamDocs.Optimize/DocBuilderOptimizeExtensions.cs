// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Optimize;

/// <summary>Builder extension that registers <see cref="OptimizePlugin"/>.</summary>
public static class DocBuilderOptimizeExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="OptimizePlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseOptimize() => builder.UsePlugin(new OptimizePlugin());

        /// <summary>Registers <see cref="OptimizePlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseOptimize(OptimizeOptions options) =>
            builder.UsePlugin(new OptimizePlugin(options));

        /// <summary>Registers <see cref="OptimizePlugin"/> with the supplied options and logger.</summary>
        /// <param name="options">Plugin options.</param>
        /// <param name="logger">Logger to receive optimize diagnostics.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseOptimize(OptimizeOptions options, ILogger logger) =>
            builder.UsePlugin(new OptimizePlugin(options, logger));

        /// <summary>Registers <see cref="HtmlMinifyPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseHtmlMinify() => builder.UsePlugin(new HtmlMinifyPlugin());

        /// <summary>Registers <see cref="HtmlMinifyPlugin"/> with the supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseHtmlMinify(HtmlMinifyOptions options) =>
            builder.UsePlugin(new HtmlMinifyPlugin(options));
    }
}
