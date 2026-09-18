// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using NuStreamDocs.Building;

namespace NuStreamDocs.SuperFences;

/// <summary>Builder-extension surface for the SuperFences dispatcher.</summary>
public static class DocBuilderSuperFencesExtensions
{
    /// <summary>Extension members for <c>DocBuilder</c>.</summary>
    /// <param name="builder">Builder to configure.</param>
    extension(DocBuilder builder)
    {
        /// <summary>Registers <see cref="SuperFencesPlugin"/> — the custom-fence dispatcher — onto <paramref name="builder"/>.</summary>
        /// <returns>The builder for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DocBuilder UseSuperFences() => builder.UsePlugin(new SuperFencesPlugin());
    }
}
