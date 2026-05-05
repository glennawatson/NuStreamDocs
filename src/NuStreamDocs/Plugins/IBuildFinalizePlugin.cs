// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the build-wide finalize phase.
/// </summary>
/// <remarks>
/// Runs once per build, sequentially in priority order, after every
/// page has been written to disk. Plugins use this hook to serialize
/// cross-page state into output files — search-index JSON, RSS/Atom
/// feeds, sitemaps, redirects, version metadata — and to perform
/// post-write transforms such as gzip compression.
/// </remarks>
public interface IBuildFinalizePlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the finalize phase.</summary>
    PluginPriority FinalizePriority { get; }

    /// <summary>Hook fired during the finalize phase.</summary>
    /// <param name="context">Per-build finalize state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> tracking the hook.</returns>
    ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken);
}
