// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.CriticMarkup;

/// <summary>
/// CriticMarkup plugin — rewrites <c>{++…++}</c>, <c>{--…--}</c>, <c>{~~old~&gt;new~~}</c>,
/// <c>{==…==}</c>, and <c>{&gt;&gt;…&lt;&lt;}</c> spans into pymdownx.critic-style HTML. Code
/// spans pass through.
/// </summary>
public sealed class CriticMarkupPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "critic"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => source.IndexOf((byte)'{') >= 0;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        CriticMarkupRewriter.Rewrite(context.Source, context.Output);
}
