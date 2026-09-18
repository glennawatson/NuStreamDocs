// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.CheckList;

/// <summary>Check-list plugin — rewrites <c>- [ ]</c> / <c>- [x]</c> tasks into disabled-checkbox HTML.</summary>
[System.Diagnostics.DebuggerDisplay("CheckListPlugin: {Name}")]
public sealed class CheckListPlugin : IPagePreRenderPlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>Stylesheet shipped with every site.</summary>
    private static readonly byte[] CssBytes =
    [
        .. """
           .task-list-item{list-style:none;margin-left:-1.4em}
           .task-list-item input[type=checkbox]{margin-right:.4em;vertical-align:middle}
           """u8
    ];

    /// <summary>Relative path for the css asset.</summary>
    private static readonly FilePath AssetFilePath = new("assets/extensions/checklist.css");

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "checklist"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets => [(AssetFilePath, CssBytes)];

    /// <summary>Gets the stylesheet link injected on every page.</summary>
    private static ReadOnlySpan<byte> LinkBytes => """<link rel="stylesheet" href="/assets/extensions/checklist.css">"""u8;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasCheckListBullet(source);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PreRender(in PagePreRenderContext context) =>
        CheckListRewriter.Rewrite(context.Source, context.Output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void WriteHeadExtra(IBufferWriter<byte> writer) => writer.Write(LinkBytes);
}
