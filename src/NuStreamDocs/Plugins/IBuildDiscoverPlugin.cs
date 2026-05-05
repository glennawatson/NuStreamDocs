// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the build-wide discovery phase.
/// </summary>
/// <remarks>
/// Runs once per build, sequentially in priority order, after configure
/// and before page enumeration. Plugins use this hook to synthesize
/// virtual pages — blog index/tag pages, generated API reference pages,
/// auto-generated tag archives — by writing markdown files under the
/// docs root so the page enumerator picks them up alongside
/// author-supplied content.
/// </remarks>
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
