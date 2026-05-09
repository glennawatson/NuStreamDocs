// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.InlineHilite;

/// <summary>Inline-highlight plugin — rewrites <c>`#!lang code`</c> inline spans into a <c>&lt;code class="highlight language-lang"&gt;</c> element for downstream syntax highlighting.</summary>
public sealed class InlineHilitePlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "inlinehilite"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasInlineHiliteFence(source);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        InlineHiliteRewriter.Rewrite(context.Source, context.Output);
}
