// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Marker contract every NuStreamDocs plugin implements.
/// </summary>
/// <remarks>
/// <para>
/// Plugins opt into individual build phases by also implementing one or
/// more of the per-phase interfaces — <see cref="IBuildConfigurePlugin"/>,
/// <see cref="IBuildDiscoverPlugin"/>, <see cref="IPagePreRenderPlugin"/>,
/// <see cref="IPagePostRenderPlugin"/>, <see cref="IPageScanPlugin"/>,
/// <see cref="IBuildResolvePlugin"/>, <see cref="IPagePostResolvePlugin"/>,
/// or <see cref="IBuildFinalizePlugin"/>. The build engine partitions
/// registered plugins into per-phase arrays at build start and only
/// iterates the participants for each phase.
/// </para>
/// <para>
/// Plugins are activated through <c>DocBuilder.UsePlugin&lt;T&gt;()</c>
/// which calls <c>new T()</c> directly — the <c>new()</c> generic
/// constraint is the AOT seam. Plugins should hold their configuration
/// in a record-typed options object passed at registration time.
/// </para>
/// </remarks>
public interface IPlugin
{
    /// <summary>Gets the human-readable plugin name as UTF-8 bytes (used in build logs and the cache fingerprint).</summary>
    ReadOnlySpan<byte> Name { get; }
}
