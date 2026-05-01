// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.SuperFences;

/// <summary>Builder-extension surface for the SuperFences dispatcher.</summary>
public static class DocBuilderSuperFencesExtensions
{
    /// <summary>Registers <see cref="SuperFencesPlugin"/> — the custom-fence dispatcher — onto <paramref name="builder"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseSuperFences(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new SuperFencesPlugin());
    }
}
