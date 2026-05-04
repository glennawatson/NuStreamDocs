// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Contract every NuStreamDocs plugin implements.
/// </summary>
/// <remarks>
/// <para>
/// Plugins are activated through <c>DocBuilder.UsePlugin&lt;T&gt;()</c>
/// (or one of the convenience <c>Use{Plugin}</c> extension methods on
/// <see cref="Building.DocBuilder"/>), which calls <c>new T()</c>
/// directly. The <c>new()</c> generic constraint is the AOT seam — no
/// reflection or <c>Activator.CreateInstance</c>. Plugins should hold
/// their configuration in a record-typed options object passed at
/// registration time and otherwise stay stateless across phases.
/// </para>
/// <para>
/// Every hook is async. Plugins that have nothing to do return
/// <see cref="ValueTask.CompletedTask"/>. Hooks return
/// <see cref="ValueTask"/> rather than <see cref="System.Threading.Tasks.Task"/>
/// because most plugins complete synchronously: <c>OnRenderPage</c>
/// runs per-page-per-plugin, so a <see cref="System.Threading.Tasks.Task"/>
/// allocation per call would dominate the hot path.
/// </para>
/// </remarks>
public interface IDocPlugin
{
    /// <summary>Gets the human-readable plugin name as UTF-8 bytes (used in build logs and the cache fingerprint).</summary>
    byte[] Name { get; }

    /// <summary>Hook fired during the configuration phase, before discovery.</summary>
    /// <param name="context">Per-build configuration state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> tracking the hook.</returns>
    ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken);

    /// <summary>Hook fired once per discovered page after parsing, before emit.</summary>
    /// <param name="context">Per-page state — read-only references plus a writable HTML output buffer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> tracking the hook.</returns>
    ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken);

    /// <summary>Hook fired after every page has been emitted.</summary>
    /// <param name="context">Per-build finalization state (search index, sitemap, feed writers).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ValueTask"/> tracking the hook.</returns>
    ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken);
}
