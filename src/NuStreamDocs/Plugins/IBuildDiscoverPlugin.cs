// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the build-wide discovery phase. Runs once per build, sequentially after
/// configure and before page enumeration; use it to synthesize virtual pages (blog indexes,
/// generated API reference, tag archives) under the docs root.
/// </summary>
public interface IBuildDiscoverPlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the discover phase.</summary>
    PluginPriority DiscoverPriority { get; }

    /// <summary>Hook fired during the discover phase.</summary>
    /// <param name="context">Per-build discovery state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> tracking the hook.</returns>
    ValueTask DiscoverAsync(BuildDiscoverContext context, CancellationToken cancellationToken);
}
