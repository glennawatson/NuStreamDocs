// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.MagicLink;

/// <summary>
/// Builder-extension surface for the magic-link plugin.
/// </summary>
public static class DocBuilderMagicLinkExtensions
{
    /// <summary>Registers <see cref="MagicLinkPlugin"/> on <paramref name="builder"/>.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseMagicLink(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new MagicLinkPlugin());
    }
}
