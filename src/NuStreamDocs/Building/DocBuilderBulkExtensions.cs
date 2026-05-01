// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Config;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Building;

/// <summary>
/// Bulk-registration helpers that wrap repeated <see cref="DocBuilder.UsePlugin(IDocPlugin)"/> /
/// <see cref="DocBuilder.UseConfigReader(IConfigReader)"/> calls for ergonomics.
/// </summary>
/// <remarks>
/// Per-plugin <c>Use{Plugin}</c> extensions ship from the plugin
/// assemblies themselves (e.g. <c>UseAutorefs</c>, <c>UseSearch</c>,
/// <c>UseNav</c>). These bulk helpers complement them — handy when a
/// host application loads a curated plugin list at startup.
/// </remarks>
public static class DocBuilderBulkExtensions
{
    /// <summary>Registers every plugin in <paramref name="plugins"/>, in order.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="plugins">Plugins to register.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UsePlugins(this DocBuilder builder, params ReadOnlySpan<IDocPlugin> plugins)
    {
        ArgumentNullException.ThrowIfNull(builder);
        for (var i = 0; i < plugins.Length; i++)
        {
            builder.UsePlugin(plugins[i]);
        }

        return builder;
    }

    /// <summary>Registers every config reader in <paramref name="readers"/>, in order.</summary>
    /// <param name="builder">Doc builder.</param>
    /// <param name="readers">Readers to register.</param>
    /// <returns>The builder for chaining.</returns>
    public static DocBuilder UseConfigReaders(this DocBuilder builder, params ReadOnlySpan<IConfigReader> readers)
    {
        ArgumentNullException.ThrowIfNull(builder);
        for (var i = 0; i < readers.Length; i++)
        {
            builder.UseConfigReader(readers[i]);
        }

        return builder;
    }
}
