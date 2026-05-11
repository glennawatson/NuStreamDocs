// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Implemented by discovery plugins that emit <see cref="SyntheticPage"/>s and want those pages
/// reflected in the navigation tree. The nav builder walks the source folder on disk, so it
/// can't see synthetic pages on its own; it scans the registered plugins for this interface and
/// grafts the supplied <see cref="SyntheticNavEntry"/>s onto the tree. Only lightweight metadata
/// is exposed here — never the page bodies — so producers can stream large page sets without the
/// nav forcing them to be retained.
/// </summary>
public interface ISyntheticNavProvider : IPlugin
{
    /// <summary>Gets the nav entries for synthetic pages this plugin contributes; populated by the time the discover phase reaches the nav plugin, empty when nothing was emitted.</summary>
    IReadOnlyList<SyntheticNavEntry> SyntheticNavEntries { get; }
}
