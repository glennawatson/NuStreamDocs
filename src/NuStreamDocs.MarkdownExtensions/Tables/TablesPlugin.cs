// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Tables;

/// <summary>
/// GitHub-flavored tables plugin. Rewrites pipe-delimited table
/// blocks into <c>&lt;table&gt;</c> HTML before the markdown
/// renderer runs.
/// </summary>
public sealed class TablesPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "tables"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        TablesRewriter.Rewrite(context.Source, context.Output);
}
