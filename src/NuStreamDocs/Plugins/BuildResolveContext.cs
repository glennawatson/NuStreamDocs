// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Read-only context handed to <see cref="IBuildResolvePlugin.ResolveAsync"/>.
/// </summary>
/// <param name="OutputRoot">Absolute path to the site output directory.</param>
/// <param name="Plugins">Every plugin registered with the builder; resolvers consult sibling registries through their owning plugin instance.</param>
/// <remarks>
/// Resolve runs once after every page completes its <see cref="IPageScanPlugin.Scan"/>
/// hook and before any page is written to disk. Plugins use this barrier
/// to finalize cross-page state (resolve dangling references, build search
/// indexes, …) so the per-page <see cref="IPagePostResolvePlugin"/> hook
/// can rewrite each page's bytes against a frozen view of the world.
/// </remarks>
public readonly record struct BuildResolveContext(
    DirectoryPath OutputRoot,
    IPlugin[] Plugins);
