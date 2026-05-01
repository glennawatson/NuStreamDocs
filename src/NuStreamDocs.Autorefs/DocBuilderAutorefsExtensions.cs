// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Autorefs;

/// <summary>Builder extension that registers <see cref="AutorefsPlugin"/>.</summary>
public static class DocBuilderAutorefsExtensions
{
    /// <summary>Registers the autorefs plugin with a fresh registry.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseAutorefs(this DocBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.UsePlugin(new AutorefsPlugin());
    }

    /// <summary>Registers the autorefs plugin against a pre-existing shared registry.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="registry">Shared registry; other plugins may already hold a reference and publish into it.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseAutorefs(this DocBuilder builder, AutorefsRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registry);
        return builder.UsePlugin(new AutorefsPlugin(registry));
    }

    /// <summary>Registers the autorefs plugin against a shared registry with a logger.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="registry">Shared registry.</param>
    /// <param name="logger">Logger forwarded to the plugin.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseAutorefs(this DocBuilder builder, AutorefsRegistry registry, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);
        return builder.UsePlugin(new AutorefsPlugin(registry, logger));
    }
}
