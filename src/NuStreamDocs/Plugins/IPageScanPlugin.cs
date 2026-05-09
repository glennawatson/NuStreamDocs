// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the per-page scan phase: read-only pass over post-render HTML used to
/// extract typed facts (heading IDs, search documents, sitemap / feed metadata) into shared
/// registries. Mutation of page bytes is forbidden in Scan — use <see
/// cref="IPagePostRenderPlugin"/> or <see cref="IPagePostResolvePlugin"/> instead.
/// </summary>
public interface IPageScanPlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the scan phase.</summary>
    PluginPriority ScanPriority { get; }

    /// <summary>Reads page bytes and publishes facts into shared registries.</summary>
    /// <param name="context">Per-page scan context.</param>
    void Scan(in PageScanContext context);
}
