// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Glossary;

/// <summary><see cref="DocBuilder"/> registration extensions for <see cref="GlossaryPlugin"/>.</summary>
public static class DocBuilderGlossaryExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Site builder.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="GlossaryPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <c>null</c>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseGlossary()
        {
            ArgumentNullException.ThrowIfNull(builder);
            return builder.UsePlugin(new GlossaryPlugin());
        }

        /// <summary>Registers <see cref="GlossaryPlugin"/> with custom <paramref name="options"/>.</summary>
        /// <param name="options">Glossary options.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <c>null</c>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseGlossary(GlossaryOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);
            return builder.UsePlugin(new GlossaryPlugin(options));
        }

        /// <summary>Registers <see cref="GlossaryPlugin"/> configured by <paramref name="configure"/>.</summary>
        /// <param name="configure">Options transform.</param>
        /// <returns>The builder for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <c>null</c>.</exception>
        public DocBuilder UseGlossary(Func<GlossaryOptions, GlossaryOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new GlossaryPlugin(configure(GlossaryOptions.Default)));
        }
    }
}
