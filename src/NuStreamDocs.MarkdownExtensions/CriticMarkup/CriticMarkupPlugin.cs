// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.CriticMarkup;

/// <summary>
/// Critic-markup plugin (pymdownx.critic). Rewrites the five
/// CriticMarkup spans into the HTML pymdownx emits:
/// <list type="bullet">
/// <item><description><c>{++ins++}</c> → <c>&lt;ins&gt;ins&lt;/ins&gt;</c></description></item>
/// <item><description><c>{--del--}</c> → <c>&lt;del&gt;del&lt;/del&gt;</c></description></item>
/// <item><description><c>{~~old~&gt;new~~}</c> → <c>&lt;del&gt;old&lt;/del&gt;&lt;ins&gt;new&lt;/ins&gt;</c></description></item>
/// <item><description><c>{==hl==}</c> → <c>&lt;mark&gt;hl&lt;/mark&gt;</c></description></item>
/// <item><description><c>{&gt;&gt;cmt&lt;&lt;}</c> → <c>&lt;span class="critic comment"&gt;cmt&lt;/span&gt;</c></description></item>
/// </list>
/// Fenced and inline code pass through verbatim.
/// </summary>
public sealed class CriticMarkupPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "critic"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        CriticMarkupRewriter.Rewrite(context.Source, context.Output);
}
