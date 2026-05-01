// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
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
public sealed class EmojiPlugin : DocPluginBase, IMarkdownPreprocessor
{
    /// <inheritdoc/>
    public override string Name => "emoji";

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        EmojiRewriter.Rewrite(source, writer);
    }
}
