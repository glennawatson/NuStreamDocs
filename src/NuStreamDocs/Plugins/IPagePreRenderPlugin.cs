// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Plugin participation in the per-page pre-render phase.
/// </summary>
/// <remarks>
/// Byte → byte rewriters on raw markdown, threaded through every
/// participating plugin in priority order before <c>MarkdownRenderer</c>
/// runs. Implemented by plugins that translate a custom block syntax
/// into HTML the renderer can pass through verbatim — admonitions
/// (<c>!!! note</c>), Material tabs (<c>=== "Tab"</c>), collapsible
/// details (<c>??? note</c>), footnotes, definition lists,
/// reference-link rewrites. The contract is byte-in / byte-out so the
/// UTF-8 pipeline stays allocation-light.
/// </remarks>
public interface IPagePreRenderPlugin : IPlugin
{
    /// <summary>Gets the plugin's bid for ordering within the pre-render phase.</summary>
    PluginPriority PreRenderPriority { get; }

    /// <summary>Determines whether <paramref name="source"/> requires rewriting.</summary>
    /// <param name="source">UTF-8 markdown bytes to be evaluated.</param>
    /// <returns><c>true</c> when the source contains markers this plugin handles; otherwise, <c>false</c>.</returns>
    bool NeedsRewrite(ReadOnlySpan<byte> source);

    /// <summary>Rewrites the markdown source, writing the result into <see cref="PagePreRenderContext.Output"/>.</summary>
    /// <param name="context">Per-page pre-render context.</param>
    void PreRender(in PagePreRenderContext context);
}
