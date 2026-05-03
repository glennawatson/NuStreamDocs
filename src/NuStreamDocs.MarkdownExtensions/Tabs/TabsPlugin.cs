// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.Tabs;

/// <summary>
/// Content-tabs plugin. Rewrites consecutive <c>=== "Title"</c>
/// blocks into a Material-flavored tabbed group (radio-button
/// driven, no JavaScript) before the markdown renderer runs.
/// </summary>
public sealed class TabsPlugin : MarkdownAssetPluginBase
{
    /// <summary>UTF-8 head-link snippet pulled into every rendered page.</summary>
    private static readonly byte[] LinkBytes =
        [.. """<link rel="stylesheet" href="/assets/extensions/tabs.css">"""u8];

    /// <summary>UTF-8 stylesheet shipped to every site.</summary>
    private static readonly byte[] CssBytes =
        [.. """
.tabbed-set{display:flex;flex-flow:row wrap;position:relative;margin:1em 0;border-radius:.2em}
.tabbed-set>input{position:absolute;opacity:0;pointer-events:none}
.tabbed-set>label{order:1;display:block;padding:.6em 1em;cursor:pointer;font-weight:700;border-bottom:2px solid transparent;color:rgba(0,0,0,.6)}
.tabbed-set>input:checked+label{border-bottom-color:#448aff;color:#448aff}
.tabbed-set>.tabbed-content{order:99;flex-basis:100%;display:none;padding:.6em 1em;border-top:1px solid rgba(0,0,0,.07)}
.tabbed-set>input:checked+label+.tabbed-content{display:block}
"""u8];

    /// <inheritdoc/>
    public override string Name => "tabs";

    /// <inheritdoc/>
    protected override FilePath AssetPath => new("assets/extensions/tabs.css");

    /// <inheritdoc/>
    protected override byte[] StylesheetBytes => CssBytes;

    /// <inheritdoc/>
    protected override byte[] HeadLink => LinkBytes;

    /// <inheritdoc/>
    public override bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasTabsOpener(source);

    /// <inheritdoc/>
    public override void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        TabsRewriter.Rewrite(source, writer);
    }
}
