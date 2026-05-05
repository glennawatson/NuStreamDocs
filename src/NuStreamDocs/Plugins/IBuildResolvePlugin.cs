// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the build-wide cross-page resolve phase.
/// </summary>
/// <remarks>
/// Runs once after every page completes its <see cref="IPageScanPlugin.Scan"/>
/// hook and before any page is written to disk. Sequential, in priority
/// order. Plugins use this barrier to finalize cross-page state — resolve
/// dangling references, build search indexes, validate links — so the
/// per-page <see cref="IPagePostResolvePlugin"/> hook can rewrite each
/// page's bytes against a frozen view of the world.
/// </remarks>
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
