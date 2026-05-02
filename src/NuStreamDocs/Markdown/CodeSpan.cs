// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;
using NuStreamDocs.Html;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Code-span handler. Recognizes matching backtick fences of any length
/// (<c>`x`</c>, <c>``x`y``</c>) and emits a <c>&lt;code&gt;</c>
/// element with the inner bytes HTML-escaped.
/// </summary>
internal static class CodeSpan
{
    /// <summary>Backtick byte.</summary>
    private const byte Backtick = (byte)'`';

    /// <summary>
    /// Handles a backtick run at <paramref name="pos"/>. Falls through
    /// to plain-text on a missing close fence so unmatched backticks
    /// render verbatim.
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the close fence on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when a complete code span was emitted.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        var fenceStart = pos;
        var fenceLength = AsciiByteHelpers.RunLength(source, pos, Backtick);
        var contentStart = fenceStart + fenceLength;

        var closeStart = FindMatchingClose(source, contentStart, fenceLength);
        if (closeStart < 0)
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, fenceStart, writer);
        Utf8StringWriter.Write(writer, "<code>"u8);
        HtmlEscape.EscapeText(source[contentStart..closeStart], writer);
        Utf8StringWriter.Write(writer, "</code>"u8);

        pos = closeStart + fenceLength;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Locates the start of a backtick run of exactly <paramref name="targetLength"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="searchFrom">First byte to consider.</param>
    /// <param name="targetLength">Required run length.</param>
    /// <returns>Start index of the matching close run, or -1.</returns>
    public static int FindMatchingClose(ReadOnlySpan<byte> source, int searchFrom, int targetLength)
    {
        var i = searchFrom;
        while (i < source.Length)
        {
            if (source[i] != Backtick)
            {
                i++;
                continue;
            }

            var runStart = i;
            var run = AsciiByteHelpers.RunLength(source, i, Backtick);
            if (run == targetLength)
            {
                return runStart;
            }

            i = runStart + run;
        }

        return -1;
    }
}
