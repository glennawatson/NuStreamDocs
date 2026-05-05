// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the per-page post-resolve phase.
/// </summary>
/// <remarks>
/// Byte → byte rewriters on rendered HTML that consume cross-page
/// state finalized in <see cref="IBuildResolvePlugin"/>. Used by
/// autoref marker resolution, redirect-marker substitution, and
/// privacy URL rewriting. Same ping-pong shape as
/// <see cref="IPagePostRenderPlugin"/>: plugins return <c>false</c>
/// from <see cref="NeedsRewrite"/> when the page contains nothing they
/// need to touch.
/// </remarks>
public interface IPagePostResolvePlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the post-resolve phase.</summary>
    PluginPriority PostResolvePriority { get; }

    /// <summary>Determines whether <paramref name="html"/> requires rewriting.</summary>
    /// <param name="html">UTF-8 HTML bytes to be evaluated.</param>
    /// <returns><c>true</c> when the HTML contains content this plugin rewrites; otherwise, <c>false</c>.</returns>
    bool NeedsRewrite(ReadOnlySpan<byte> html);

    /// <summary>Rewrites the resolved HTML, writing the result into <see cref="PagePostResolveContext.Output"/>.</summary>
    /// <param name="context">Per-page post-resolve context.</param>
    void Rewrite(in PagePostResolveContext context);
}
