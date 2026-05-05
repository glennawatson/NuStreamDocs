// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.AttrList;

/// <summary>
/// Block-level attr-list plugin. Recognizes a trailing
/// <c>{: #id .class key=value}</c> token at the end of a block
/// element's text and lifts its tokens into HTML attributes on the
/// element's opening tag (the equivalent of pmdownx <c>attr_list</c>'s
/// block syntax).
/// </summary>
/// <remarks>
/// Runs as a post-render HTML rewriter rather than a source
/// preprocessor: by the time markdown rendering has produced
/// <c>&lt;h1&gt;Heading {: .my-class }&lt;/h1&gt;</c> we can detect
/// the pattern unambiguously without re-parsing markdown. Block
/// elements covered: <c>h1-h6</c>, <c>p</c>, <c>li</c>, <c>td</c>,
/// <c>th</c>, <c>dd</c>, <c>dt</c>, <c>blockquote</c>. Inline
/// attr-list (e.g. <c>[Link]{: target="_blank" }</c>) is a
/// follow-up — it needs hooks earlier in the rendering pipeline.
/// </remarks>
public sealed class AttrListPlugin : IPagePostRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "attr-list"u8;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html) => AttrListRewriter.NeedsRewrite(html);

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context) =>
        AttrListRewriter.RewriteInto(context.Html, context.Output);
}
