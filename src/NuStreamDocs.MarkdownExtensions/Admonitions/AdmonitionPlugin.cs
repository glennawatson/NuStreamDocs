// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.Admonitions;

/// <summary>
/// Admonition plugin. Rewrites
/// <c>!!! type "Optional title"</c> blocks into
/// <c>&lt;div class="admonition type"&gt;</c> HTML before the markdown
/// renderer runs, ships <c>admonition.css</c> as a static asset, and
/// pulls it in via the page <c>&lt;head&gt;</c>.
/// </summary>
public sealed class AdmonitionPlugin : IPagePreRenderPlugin, IStaticAssetProvider, IHeadExtraProvider
{
    /// <summary>UTF-8 head-link snippet pulled into every rendered page.</summary>
    private static readonly byte[] LinkBytes =
        [.. """<link rel="stylesheet" href="/assets/extensions/admonition.css">"""u8];

    /// <summary>UTF-8 stylesheet shipped to every site.</summary>
    private static readonly byte[] CssBytes =
        [.. """
            .admonition{border-left:4px solid #448aff;background:rgba(68,138,255,.1);padding:.6em 1em;margin:1em 0;border-radius:.2em}
            .admonition .admonition-title{font-weight:700;margin:0 0 .4em}
            .admonition.note{border-left-color:#448aff;background:rgba(68,138,255,.1)}
            .admonition.tip,.admonition.hint{border-left-color:#00bfa5;background:rgba(0,191,165,.1)}
            .admonition.warning,.admonition.caution,.admonition.attention{border-left-color:#ff9100;background:rgba(255,145,0,.1)}
            .admonition.danger,.admonition.error{border-left-color:#ff1744;background:rgba(255,23,68,.1)}
            .admonition.info,.admonition.todo{border-left-color:#00b8d4;background:rgba(0,184,212,.1)}
            .admonition.success,.admonition.check{border-left-color:#00c853;background:rgba(0,200,83,.1)}
            .admonition.quote,.admonition.cite{border-left-color:#9e9e9e;background:rgba(158,158,158,.1)}
            """u8];

    /// <summary>Forward-slash relative path the css asset is written to.</summary>
    private static readonly FilePath AssetFilePath = new("assets/extensions/admonition.css");

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "admonitions"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public (FilePath Path, byte[] Bytes)[] StaticAssets => [(AssetFilePath, CssBytes)];

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasAdmonitionOpener(source);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        AdmonitionRewriter.Rewrite(context.Source, context.Output);

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.Write(LinkBytes);
    }
}
