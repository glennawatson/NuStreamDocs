// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Emoji;

/// <summary>
/// Stateless UTF-8 emoji-shortcode rewriter. Walks the source byte
/// stream, locating <c>:identifier:</c> shortcodes whose body is
/// alphanumeric / underscore / plus / minus / hyphen / dot, and
/// rewriting matched ones into a <c>&lt;span class="twemoji"&gt;</c>
/// wrapper. Fenced and inline-code regions pass through verbatim;
/// unknown shortcodes are left untouched.
/// </summary>
internal static class EmojiRewriter
{
    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        CodeAwareRewriter.Run(source, writer, TryRewriteShortcode);

    /// <summary>Tries to match a <c>:shortcode:</c> at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True when a known shortcode was rewritten.</returns>
    private static bool TryRewriteShortcode(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        if (source[offset] is not (byte)':')
        {
            return false;
        }

        var bodyStart = offset + 1;
        if (bodyStart >= source.Length || !ShortcodeScanner.IsBodyByte(source[bodyStart]))
        {
            return false;
        }

        var bodyEnd = ShortcodeScanner.ScanBody(source, bodyStart);
        if (bodyEnd >= source.Length || source[bodyEnd] is not (byte)':')
        {
            return false;
        }

        if (!EmojiIndex.TryGet(source[bodyStart..bodyEnd], out var glyph))
        {
            return false;
        }

        writer.Write("<span class=\"twemoji\">"u8);
        writer.Write(glyph);
        writer.Write("</span>"u8);
        consumed = bodyEnd + 1 - offset;
        return true;
    }
}
