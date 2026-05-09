// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.MdInHtml;

/// <summary>Markdown-in-HTML plugin — strips <c>markdown="1"</c> / <c>markdown="block"</c> attributes from HTML blocks and pads the inner content so the body parses as Markdown.</summary>
public sealed class MdInHtmlPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "md_in_html"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasMdInHtmlAttribute(source);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        MdInHtmlRewriter.Rewrite(context.Source, context.Output);
}
