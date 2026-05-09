// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Arithmatex;

/// <summary>
/// Arithmatex plugin (pymdownx.arithmatex generic mode); rewrites <c>$x$</c> / <c>$$x$$</c> math spans into
/// <c>&lt;span class="arithmatex"&gt;\(x\)&lt;/span&gt;</c> / <c>&lt;div class="arithmatex"&gt;\[x\]&lt;/div&gt;</c>
/// for a client-side MathJax/KaTeX renderer.
/// </summary>
/// <remarks>
/// Inline boundaries follow pymdownx defaults: opening <c>$</c> must not be followed by whitespace, closing <c>$</c>
/// must not be preceded by whitespace and must not be followed by a digit (so prices like <c>$5</c> never trigger).
/// Fenced and inline-code regions pass through verbatim.
/// </remarks>
public sealed class ArithmatexPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "arithmatex"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => source.IndexOf((byte)'$') >= 0;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        ArithmatexRewriter.Rewrite(context.Source, context.Output);
}
