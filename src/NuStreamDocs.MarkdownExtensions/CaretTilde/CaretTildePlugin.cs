// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.MarkdownExtensions.CaretTilde;

/// <summary>
/// Caret + tilde plugin — rewrites <c>^x^</c> / <c>^^x^^</c> / <c>~x~</c> / <c>~~x~~</c> into
/// <c>&lt;sup&gt;</c> / <c>&lt;ins&gt;</c> / <c>&lt;sub&gt;</c> / <c>&lt;del&gt;</c>. Code spans
/// pass through.
/// </summary>
public sealed class CaretTildePlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "caret-tilde"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        MarkdownMarkerProbes.HasCaretOrTilde(source);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        CaretTildeRewriter.Rewrite(context.Source, context.Output);
}
