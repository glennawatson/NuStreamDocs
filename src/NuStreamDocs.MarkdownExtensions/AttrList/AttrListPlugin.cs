// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.AttrList;

/// <summary>
/// Block attr-list plugin — lifts a trailing <c>{: #id .class key=value}</c> token into HTML
/// attributes on the enclosing block element. Covers <c>h1-h6</c>, <c>p</c>, <c>li</c>,
/// <c>td</c>, <c>th</c>, <c>dd</c>, <c>dt</c>, <c>blockquote</c>.
/// </summary>
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
