// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Html;

/// <summary>Marks every <c>&lt;script&gt;</c> element in a page with <c>data-cfasync="false"</c> so Cloudflare Rocket Loader leaves it alone.</summary>
internal static class RocketLoaderOptOutRewriter
{
    /// <summary>Gets the script start-tag prefix.</summary>
    private static ReadOnlySpan<byte> ScriptOpen => "<script"u8;

    /// <summary>Gets the script end-tag prefix.</summary>
    private static ReadOnlySpan<byte> ScriptClose => "</script"u8;

    /// <summary>Gets the attribute name Rocket Loader honors.</summary>
    private static ReadOnlySpan<byte> CfAsyncName => "data-cfasync"u8;

    /// <summary>Gets the attribute inserted after the tag name.</summary>
    private static ReadOnlySpan<byte> OptOutAttribute => " data-cfasync=\"false\""u8;

    /// <summary>Returns true when <paramref name="html"/> may contain a script element.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <returns>True when a rewrite pass is needed.</returns>
    internal static bool NeedsRewrite(ReadOnlySpan<byte> html) => html.IndexOf(ScriptOpen) >= 0;

    /// <summary>Copies <paramref name="html"/> to <paramref name="output"/>, adding the opt-out attribute to script start tags that lack one.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="output">Destination.</param>
    internal static void Rewrite(ReadOnlySpan<byte> html, IBufferWriter<byte> output)
    {
        var cursor = 0;
        while (cursor < html.Length)
        {
            var idx = html[cursor..].IndexOf(ScriptOpen);
            if (idx < 0)
            {
                break;
            }

            var nameEnd = cursor + idx + ScriptOpen.Length;
            if (nameEnd >= html.Length || !IsTagNameTerminator(html[nameEnd]))
            {
                output.Write(html[cursor..nameEnd]);
                cursor = nameEnd;
                continue;
            }

            var tagEnd = FindTagEnd(html, nameEnd);
            if (tagEnd < 0)
            {
                break;
            }

            output.Write(html[cursor..nameEnd]);
            if (html[nameEnd..tagEnd].IndexOf(CfAsyncName) < 0)
            {
                output.Write(OptOutAttribute);
            }

            // The script body is raw text; copy it through untouched so a "<script" inside a JS string isn't rewritten.
            var bodyStart = tagEnd + 1;
            var close = html[bodyStart..].IndexOf(ScriptClose);
            var resume = close < 0 ? html.Length : bodyStart + close + ScriptClose.Length;
            output.Write(html[nameEnd..resume]);
            cursor = resume;
        }

        output.Write(html[cursor..]);
    }

    /// <summary>Returns true when <paramref name="b"/> ends a tag name.</summary>
    /// <param name="b">Byte following <c>&lt;script</c>.</param>
    /// <returns>True for whitespace, <c>&gt;</c> or <c>/</c>.</returns>
    private static bool IsTagNameTerminator(byte b) => b is (byte)'>' or (byte)'/' || AsciiByteHelpers.IsAsciiWhitespace(b);

    /// <summary>Finds the <c>&gt;</c> closing a start tag, skipping quoted attribute values.</summary>
    /// <param name="html">UTF-8 page HTML.</param>
    /// <param name="start">Offset just past the tag name.</param>
    /// <returns>The offset of the closing <c>&gt;</c>, or -1 when the tag is unterminated.</returns>
    private static int FindTagEnd(ReadOnlySpan<byte> html, int start)
    {
        byte quote = 0;
        for (var i = start; i < html.Length; i++)
        {
            var b = html[i];
            if (quote is not 0)
            {
                if (b == quote)
                {
                    quote = 0;
                }

                continue;
            }

            if (b is (byte)'"' or (byte)'\'')
            {
                quote = b;
                continue;
            }

            if (b is (byte)'>')
            {
                return i;
            }
        }

        return -1;
    }
}
