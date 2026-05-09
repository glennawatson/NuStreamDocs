// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Keys;

/// <summary>Keys plugin — rewrites <c>++ctrl+alt+del++</c> shortcuts into pymdownx.keys-style <c>&lt;span class="keys"&gt;</c> markup. Unknown tokens render with a sanitized class.</summary>
public sealed class KeysPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "keys"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => source.IndexOf("++"u8) >= 0;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        KeysRewriter.Rewrite(context.Source, context.Output);
}
