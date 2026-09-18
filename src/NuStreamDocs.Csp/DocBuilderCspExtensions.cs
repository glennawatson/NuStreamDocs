// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Csp;

/// <summary>Builder-extension surface for <see cref="CspPlugin"/>.</summary>
public static class DocBuilderCspExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="CspPlugin"/> with default options.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseCsp() => builder.UsePlugin(new CspPlugin());

        /// <summary>Registers <see cref="CspPlugin"/> with caller-tweaked options.</summary>
        /// <param name="configure">Function that receives <see cref="CspOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseCsp(Func<CspOptions, CspOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new CspPlugin(configure(CspOptions.Default)));
        }

        /// <summary>Registers <see cref="CspPlugin"/> with caller-supplied options.</summary>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseCsp(in CspOptions options) =>
            builder.UsePlugin(new CspPlugin(options));
    }
}
