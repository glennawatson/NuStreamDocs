// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Tabs;

/// <summary>Content-tabs plugin — rewrites <c>=== "Title"</c> blocks into a radio-button-driven tabbed group.</summary>
public sealed class TabsPlugin : IPagePreRenderPlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>Head-link snippet injected on every page.</summary>
    private static readonly byte[] LinkBytes =
        [.. """<link rel="stylesheet" href="/assets/extensions/tabs.css">"""u8];

    /// <summary>Stylesheet shipped with every site.</summary>
    private static readonly byte[] CssBytes =
        [.. """
.tabbed-set{display:flex;flex-flow:row wrap;position:relative;margin:1em 0;border-radius:.2em}
.tabbed-set>input{position:absolute;opacity:0;pointer-events:none}
.tabbed-set>label{order:1;display:block;padding:.6em 1em;cursor:pointer;font-weight:700;border-bottom:2px solid transparent;color:rgba(0,0,0,.6)}
.tabbed-set>input:checked+label{border-bottom-color:#448aff;color:#448aff}
.tabbed-set>.tabbed-content{order:99;flex-basis:100%;display:none;padding:.6em 1em;border-top:1px solid rgba(0,0,0,.07)}
.tabbed-set>input:checked+label+.tabbed-content{display:block}
"""u8];

    /// <summary>Relative path for the css asset.</summary>
    private static readonly FilePath AssetFilePath = new("assets/extensions/tabs.css");

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "tabs"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets => [(AssetFilePath, CssBytes)];

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasTabsOpener(source);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        TabsRewriter.Rewrite(context.Source, context.Output);

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(LinkBytes);
    }
}
