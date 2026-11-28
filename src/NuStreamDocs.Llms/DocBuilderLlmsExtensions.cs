// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.Llms;

/// <summary>Builder-extension surface for <see cref="LlmsPlugin"/>.</summary>
public static class DocBuilderLlmsExtensions
{
    /// <summary>Extension members for <see cref="DocBuilder"/>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="LlmsPlugin"/> with <see cref="LlmsOptions.Default"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLlms() =>
            builder.UsePlugin(new LlmsPlugin());

        /// <summary>Registers <see cref="LlmsPlugin"/> with the supplied options.</summary>
        /// <param name="options">LLMs output options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseLlms(in LlmsOptions options) =>
            builder.UsePlugin(new LlmsPlugin(options));

        /// <summary>Registers <see cref="LlmsPlugin"/> with options produced from <see cref="LlmsOptions.Default"/>.</summary>
        /// <param name="configure">Callback receiving <see cref="LlmsOptions.Default"/> and returning customized options.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseLlms(Func<LlmsOptions, LlmsOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            return builder.UsePlugin(new LlmsPlugin(configure(LlmsOptions.Default)));
        }
    }
}
