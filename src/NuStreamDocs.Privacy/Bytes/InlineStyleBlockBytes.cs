// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Byte-level scanner that finds <c>&lt;style&gt;...&lt;/style&gt;</c>
/// blocks and rewrites every <c>url(...)</c> token inside the block
/// body via <see cref="CssUrlBytes"/>. Replaces <c>InlineStyleRegex</c>.
/// </summary>
internal static class InlineStyleBlockBytes
{
    /// <summary>Bytes that may start a <c>&lt;style</c> opening tag (case-insensitive).</summary>
    private static readonly SearchValues<byte> StyleStart = SearchValues.Create("<"u8);

    /// <summary>Gets the lowercase <c>&lt;style</c> opening sequence (sans tag-attr boundary).</summary>
    private static ReadOnlySpan<byte> StyleOpen => "<style"u8;

    /// <summary>Gets the lowercase closing tag <c>&lt;/style&gt;</c>.</summary>
    private static ReadOnlySpan<byte> StyleClose => "</style>"u8;

    /// <summary>Walks <paramref name="html"/>, copying through verbatim, but rewriting every <c>url(...)</c> token inside <c>&lt;style&gt;</c> bodies.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <returns>True when at least one URL inside an inline-style body was rewritten.</returns>
    public static bool RewriteInto(ReadOnlySpan<byte> html, in UrlRewriteContext ctx, IBufferWriter<byte> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        var changed = false;
        var lastEmit = 0;
        var cursor = 0;
        while (cursor < html.Length)
        {
            var rel = html[cursor..].IndexOfAny(StyleStart);
            if (rel < 0)
            {
                break;
            }

            var p = cursor + rel;
            if (TryRewriteBlock(html, p, ctx, sink, ref lastEmit, out var advanceTo))
            {
                changed = true;
            }

            cursor = advanceTo;
        }

        if (!changed)
        {
            return false;
        }

        sink.Write(html[lastEmit..]);
        return true;
    }

    /// <summary>Tries to rewrite a single <c>&lt;style&gt;</c> block at <paramref name="p"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset of <c>&lt;</c>.</param>
    /// <param name="ctx">URL-rewrite context.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="lastEmit">Source offset emitted up to.</param>
    /// <param name="advanceTo">Offset to resume scanning from.</param>
    /// <returns>True when at least one url(...) inside the body was rewritten.</returns>
    private static bool TryRewriteBlock(ReadOnlySpan<byte> html, int p, in UrlRewriteContext ctx, IBufferWriter<byte> sink, ref int lastEmit, out int advanceTo)
    {
        if (!TryMatchStyleBlock(html, p, out var bodyStart, out var bodyEnd, out var blockEnd))
        {
            advanceTo = p + 1;
            return false;
        }

        var temp = new ArrayBufferWriter<byte>(bodyEnd - bodyStart);
        if (!CssUrlBytes.RewriteInto(html[bodyStart..bodyEnd], ctx, temp))
        {
            advanceTo = blockEnd;
            return false;
        }

        sink.Write(html[lastEmit..bodyStart]);
        sink.Write(temp.WrittenSpan);
        lastEmit = bodyEnd;
        advanceTo = blockEnd;
        return true;
    }

    /// <summary>Validates a <c>&lt;style ...&gt;...&lt;/style&gt;</c> block at <paramref name="p"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="p">Candidate offset.</param>
    /// <param name="bodyStart">First body byte (just past the opening <c>&gt;</c>).</param>
    /// <param name="bodyEnd">Offset of the closing <c>&lt;/style&gt;</c>.</param>
    /// <param name="blockEnd">Offset just past the closing tag.</param>
    /// <returns>True on a successful match.</returns>
    private static bool TryMatchStyleBlock(ReadOnlySpan<byte> html, int p, out int bodyStart, out int bodyEnd, out int blockEnd)
    {
        bodyStart = -1;
        bodyEnd = -1;
        blockEnd = -1;
        if (!ByteHelpers.StartsWithIgnoreAsciiCase(html, p, StyleOpen))
        {
            return false;
        }

        var afterName = p + StyleOpen.Length;
        if (afterName >= html.Length || ByteHelpers.IsAsciiIdentifierByte(html[afterName]))
        {
            return false;
        }

        var openEndRel = html[afterName..].IndexOf((byte)'>');
        if (openEndRel < 0)
        {
            return false;
        }

        bodyStart = afterName + openEndRel + 1;
        var closeRel = IndexOfStyleClose(html, bodyStart);
        if (closeRel < 0)
        {
            return false;
        }

        bodyEnd = closeRel;
        blockEnd = closeRel + StyleClose.Length;
        return true;
    }

    /// <summary>Searches for <c>&lt;/style&gt;</c> case-insensitively starting at <paramref name="from"/>.</summary>
    /// <param name="html">UTF-8 source.</param>
    /// <param name="from">Search start offset.</param>
    /// <returns>Offset of the closing tag, or <c>-1</c>.</returns>
    private static int IndexOfStyleClose(ReadOnlySpan<byte> html, int from)
    {
        for (var i = from; i + StyleClose.Length <= html.Length; i++)
        {
            if (ByteHelpers.StartsWithIgnoreAsciiCase(html, i, StyleClose))
            {
                return i;
            }
        }

        return -1;
    }
}
