// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Autorefs;
using NuStreamDocs.Building;

namespace NuStreamDocs.Xrefs;

/// <summary>Builder extension that registers <see cref="XrefsPlugin"/>.</summary>
public static class DocBuilderXrefsExtensions
{
    /// <summary>Registers <see cref="XrefsPlugin"/> with default options and a fresh registry.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseXrefs(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new XrefsPlugin());
    }

    /// <summary>Registers <see cref="XrefsPlugin"/> with the supplied options and a fresh registry.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseXrefs(this DocBuilder builder, XrefsOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new XrefsPlugin(new(), options));
    }

    /// <summary>Registers <see cref="XrefsPlugin"/> sharing <paramref name="registry"/> with another plugin (typically <c>AutorefsPlugin</c>).</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="registry">Shared autorefs registry.</param>
    /// <param name="options">Plugin options.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseXrefs(this DocBuilder builder, AutorefsRegistry registry, XrefsOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(options);
        return builder.UsePlugin(new XrefsPlugin(registry, options));
    }
}
