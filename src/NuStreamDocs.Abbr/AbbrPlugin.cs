// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Abbr;

/// <summary>Abbreviation plugin — folds <c>*[token]: definition</c> lines into <c>&lt;abbr title="..."&gt;</c> tags. Code spans are skipped.</summary>
public sealed class AbbrPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "abbr"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => source.IndexOf("*["u8) >= 0;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        AbbrRewriter.Rewrite(context.Source, context.Output);
}
