// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Hard-break handler. Recognises two-or-more trailing spaces before a
/// newline and emits <c>&lt;br /&gt;\n</c>.
/// </summary>
internal static class HardBreak
{
    /// <summary>Space byte.</summary>
    private const byte Sp = (byte)' ';

    /// <summary>Line-feed byte.</summary>
    private const byte Lf = (byte)'\n';

    /// <summary>Minimum trailing spaces required to form a hard break.</summary>
    private const int MinTrailingSpaces = 2;

    /// <summary>
    /// Handles a space at <paramref name="pos"/>.
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the LF on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when a hard break was emitted.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        var spaceStart = pos;
        var i = pos;
        while (i < source.Length && source[i] == Sp)
        {
            i++;
        }

        var spaceCount = i - spaceStart;
        if (spaceCount < MinTrailingSpaces || i >= source.Length || source[i] != Lf)
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, spaceStart, writer);

        var dst = writer.GetSpan("<br />\n"u8.Length);
        "<br />\n"u8.CopyTo(dst);
        writer.Advance("<br />\n"u8.Length);

        pos = i + 1;
        pendingTextStart = pos;
        return true;
    }
}
