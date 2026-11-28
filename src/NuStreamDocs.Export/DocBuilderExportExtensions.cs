// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Export;

/// <summary><see cref="DocBuilder"/> registration extensions for <see cref="ExportPlugin"/>.</summary>
public static class DocBuilderExportExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Site builder.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="ExportPlugin"/> with default options (<c>/print.html</c>).</summary>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseExport()
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.UsePlugin(new ExportPlugin());
        }

        /// <summary>Registers <see cref="ExportPlugin"/> with <paramref name="options"/>.</summary>
        /// <param name="options">Export options.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <c>null</c>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseExport(ExportOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);
            return builder.UsePlugin(new ExportPlugin(options));
        }

        /// <summary>Registers <see cref="ExportPlugin"/> configured via <paramref name="configure"/>.</summary>
        /// <param name="configure">Options transform.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <c>null</c>.</exception>
        public DocBuilder UseExport(Func<ExportOptions, ExportOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new ExportPlugin(configure(ExportOptions.Default)));
        }
    }
}
