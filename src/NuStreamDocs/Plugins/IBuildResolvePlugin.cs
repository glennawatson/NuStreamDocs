// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the build-wide cross-page resolve barrier. Runs sequentially after
/// every page's <see cref="IPageScanPlugin.Scan"/> hook and before any page is written; use it to
/// finalize cross-page state so <see cref="IPagePostResolvePlugin"/> hooks can rewrite each page
/// against a frozen view.
/// </summary>
public interface IBuildResolvePlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the resolve phase.</summary>
    PluginPriority ResolvePriority { get; }

    /// <summary>Hook fired during the cross-page resolve barrier.</summary>
    /// <param name="context">Per-build resolve state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> tracking the hook.</returns>
    ValueTask ResolveAsync(BuildResolveContext context, CancellationToken cancellationToken);
}
