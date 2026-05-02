// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
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
        var fenceLength = RunLength(source, pos);
        var contentStart = fenceStart + fenceLength;

        var closeStart = FindMatchingClose(source, contentStart, fenceLength);
        if (closeStart < 0)
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, fenceStart, writer);
        Write("<code>"u8, writer);
        HtmlEscape.EscapeText(source[contentStart..closeStart], writer);
        Write("</code>"u8, writer);

        pos = closeStart + fenceLength;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Counts the run of backticks starting at <paramref name="pos"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Run start.</param>
    /// <returns>Run length in bytes.</returns>
    public static int RunLength(ReadOnlySpan<byte> source, int pos)
    {
        var i = pos;
        while (i < source.Length && source[i] == Backtick)
        {
            i++;
        }

        return i - pos;
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
            var run = RunLength(source, i);
            if (run == targetLength)
            {
                return runStart;
            }

            i = runStart + run;
        }

        return -1;
    }

    /// <summary>Bulk-writes UTF-8 bytes to <paramref name="writer"/>.</summary>
    /// <param name="bytes">Bytes to write.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void Write(ReadOnlySpan<byte> bytes, IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }
}
