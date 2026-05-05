// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Plugins;

namespace NuStreamDocs.Emoji;

/// <summary>
/// Emoji shortcode plugin. Turns <c>:name:</c> shortcodes into
/// <c>&lt;span class="twemoji"&gt;…&lt;/span&gt;</c> wrappers
/// around the matching Unicode glyph, matching the pymdownx.emoji
/// default shape Zensical configures.
/// </summary>
/// <remarks>
/// The bundled <see cref="EmojiIndex"/> ships ~80 of the most
/// common shortcodes. Unknown shortcodes pass through verbatim so
/// they remain visible to downstream processors (an icon pack, a
/// reverse-proxy renderer, or simply human eyes).
/// </remarks>
public sealed class EmojiPlugin : IPagePreRenderPlugin
{
    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "emoji"u8;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) => true;

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        EmojiRewriter.Rewrite(context.Source, context.Output);
}
