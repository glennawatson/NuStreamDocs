// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Emoji;

/// <summary>Emoji shortcode plugin — rewrites known <c>:name:</c> shortcodes into <c>&lt;span class="twemoji"&gt;</c> wrappers. Unknown shortcodes pass through.</summary>
public sealed class EmojiPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "emoji"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => source.IndexOf((byte)':') >= 0;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        EmojiRewriter.Rewrite(context.Source, context.Output);
}
