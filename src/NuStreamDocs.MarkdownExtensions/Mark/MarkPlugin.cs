// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Mark;

/// <summary>Mark plugin — rewrites <c>==text==</c> spans into <c>&lt;mark&gt;</c>. Code spans are skipped.</summary>
public sealed class MarkPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "mark"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasMarkSpan(source);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        MarkRewriter.Rewrite(context.Source, context.Output);
}
