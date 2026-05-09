// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Marker contract every NuStreamDocs plugin implements. Plugins opt into individual build phases
/// by also implementing one or more per-phase interfaces (<see cref="IBuildConfigurePlugin"/>,
/// <see cref="IBuildDiscoverPlugin"/>, <see cref="IPagePreRenderPlugin"/>, <see
/// cref="IPagePostRenderPlugin"/>, <see cref="IPageScanPlugin"/>, <see
/// cref="IBuildResolvePlugin"/>, <see cref="IPagePostResolvePlugin"/>, <see
/// cref="IBuildFinalizePlugin"/>).
/// </summary>
public interface IPlugin
{
    /// <summary>Gets the human-readable plugin name as UTF-8 bytes (used in build logs and the cache fingerprint).</summary>
    ReadOnlySpan<byte> Name { get; }
}
