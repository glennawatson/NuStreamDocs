// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the build-wide configuration phase. Runs once per build, sequentially
/// in priority order, before discovery; use it to seed shared registries, register cross-page
/// markers, and read site-wide options.
/// </summary>
public interface IBuildConfigurePlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the configure phase.</summary>
    PluginPriority ConfigurePriority { get; }

    /// <summary>Hook fired during the configure phase.</summary>
    /// <param name="context">Per-build configuration state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> tracking the hook.</returns>
    ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken);
}
