// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Macros;

/// <summary>Builder-extension surface for the macros plugin.</summary>
public static class DocBuilderMacrosExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="MacrosPlugin"/> with the default option set.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMacros() => builder.UsePlugin(new MacrosPlugin());

        /// <summary>Registers <see cref="MacrosPlugin"/> with options-customization.</summary>
        /// <param name="configure">Function that receives <see cref="MacrosOptions.Default"/> and returns the customized set.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseMacros(Func<MacrosOptions, MacrosOptions> configure)
        {
            var options = configure(MacrosOptions.Default);
            return builder.UsePlugin(new MacrosPlugin(options));
        }

        /// <summary>Registers <see cref="MacrosPlugin"/> with options + logger.</summary>
        /// <param name="configure">Options customization.</param>
        /// <param name="logger">Logger that receives missing-variable warnings.</param>
        /// <returns>The builder for chaining.</returns>
        public DocBuilder UseMacros(
            Func<MacrosOptions, MacrosOptions> configure,
            ILogger logger)
        {
            var options = configure(MacrosOptions.Default);
            return builder.UsePlugin(new MacrosPlugin(options, logger));
        }

        /// <summary>Convenience overload — pre-built options + logger.</summary>
        /// <param name="options">Resolved options.</param>
        /// <param name="logger">Logger.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMacros(MacrosOptions options, ILogger logger) =>
            builder.UsePlugin(new MacrosPlugin(options, logger));

        /// <summary>Convenience overload — pre-built options, no logger.</summary>
        /// <param name="options">Resolved options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseMacros(MacrosOptions options) =>
            builder.UseMacros(options, NullLogger.Instance);
    }
}
