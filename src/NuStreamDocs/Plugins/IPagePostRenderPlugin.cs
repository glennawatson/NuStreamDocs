// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the per-page post-render phase.
/// </summary>
/// <remarks>
/// Byte → byte rewriters on rendered HTML, threaded through every
/// participating plugin in priority order before the per-page
/// <see cref="IPageScanPlugin"/> hook fires. Used by theme shell
/// substitution, code-block highlighters, mermaid fence retagging,
/// nav-marker substitution, lightbox image wrapping, and similar
/// HTML-aware rewriters. Plugins return <c>false</c> from
/// <see cref="NeedsRewrite"/> when the page contains nothing they need
/// to touch — the engine then skips both the buffer swap and the call
/// to <see cref="PostRender"/>.
/// </remarks>
public interface IPagePostRenderPlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the post-render phase.</summary>
    PluginPriority PostRenderPriority { get; }

    /// <summary>Determines whether <paramref name="html"/> requires rewriting.</summary>
    /// <param name="html">UTF-8 HTML bytes to be evaluated.</param>
    /// <returns><c>true</c> when the HTML contains content this plugin rewrites; otherwise, <c>false</c>.</returns>
    bool NeedsRewrite(ReadOnlySpan<byte> html);

    /// <summary>Rewrites the rendered HTML, writing the result into <see cref="PagePostRenderContext.Output"/>.</summary>
    /// <param name="context">Per-page post-render context.</param>
    void PostRender(in PagePostRenderContext context);
}
