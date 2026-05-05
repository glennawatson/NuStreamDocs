// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.CheckList;

/// <summary>
/// Check-list plugin. Rewrites <c>- [ ] item</c> and <c>- [x] item</c>
/// at list-item starts into inline disabled-checkbox HTML so the
/// markdown renderer passes them through unchanged.
/// </summary>
public sealed class CheckListPlugin : IPagePreRenderPlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>UTF-8 head-link snippet pulled into every rendered page.</summary>
    private static readonly byte[] LinkBytes =
        [.. """<link rel="stylesheet" href="/assets/extensions/checklist.css">"""u8];

    /// <summary>UTF-8 stylesheet shipped to every site.</summary>
    private static readonly byte[] CssBytes =
        [.. """
.task-list-item{list-style:none;margin-left:-1.4em}
.task-list-item input[type=checkbox]{margin-right:.4em;vertical-align:middle}
"""u8];

    /// <summary>Forward-slash relative path the css asset is written to.</summary>
    private static readonly FilePath AssetFilePath = new("assets/extensions/checklist.css");

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "checklist"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets => [(AssetFilePath, CssBytes)];

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasCheckListBullet(source);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        CheckListRewriter.Rewrite(context.Source, context.Output);

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(LinkBytes);
    }
}
