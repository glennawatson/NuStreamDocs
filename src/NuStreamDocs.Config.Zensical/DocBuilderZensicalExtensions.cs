// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Config.Zensical;

/// <summary>Builder-extension surface for the Zensical TOML config reader.</summary>
public static class DocBuilderZensicalExtensions
{
    /// <summary>Registers <see cref="ZensicalConfigReader"/> with the builder.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseZensicalConfig(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UseConfigReader(new ZensicalConfigReader());
    }
}
