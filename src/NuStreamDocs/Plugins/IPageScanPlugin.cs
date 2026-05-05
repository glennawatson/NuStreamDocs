// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the per-page scan phase.
/// </summary>
/// <remarks>
/// Read-only pass over the post-render HTML. Plugins extract typed
/// facts (heading IDs, search documents, sitemap entries, feed
/// metadata) and publish into shared registries owned by their plugin
/// instance. The pass is synchronous — implementations must stay cheap
/// because Scan runs on the per-page hot path with parallelism.
/// Mutation of page bytes is forbidden in Scan; use
/// <see cref="IPagePostRenderPlugin"/> (before the cross-page barrier)
/// or <see cref="IPagePostResolvePlugin"/> (after) instead.
/// </remarks>
public interface IPageScanPlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the scan phase.</summary>
    PluginPriority ScanPriority { get; }

    /// <summary>Reads page bytes and publishes facts into shared registries.</summary>
    /// <param name="context">Per-page scan context.</param>
    void Scan(in PageScanContext context);
}
