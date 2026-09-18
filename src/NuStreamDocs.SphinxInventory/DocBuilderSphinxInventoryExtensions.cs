// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Autorefs;
using NuStreamDocs.Building;

namespace NuStreamDocs.SphinxInventory;

/// <summary>Builder-extension surface for <see cref="SphinxInventoryPlugin"/>.</summary>
public static class DocBuilderSphinxInventoryExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder receiving the inventory plugin.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="SphinxInventoryPlugin"/> with default options and a fresh registry.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSphinxInventory() =>
            builder.UsePlugin(new SphinxInventoryPlugin());

        /// <summary>Registers <see cref="SphinxInventoryPlugin"/> with the supplied registry and default options.</summary>
        /// <param name="registry">Shared autorefs registry.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSphinxInventory(AutorefsRegistry registry) =>
            builder.UsePlugin(new SphinxInventoryPlugin(registry));

        /// <summary>Registers <see cref="SphinxInventoryPlugin"/> with the supplied registry and options.</summary>
        /// <param name="registry">Shared autorefs registry.</param>
        /// <param name="options">Plugin options.</param>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSphinxInventory(
            AutorefsRegistry registry,
            SphinxInventoryOptions options) => builder.UsePlugin(new SphinxInventoryPlugin(registry, options));
    }
}
