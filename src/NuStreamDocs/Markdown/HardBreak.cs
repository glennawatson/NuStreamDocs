// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Hard-break handler. Recognizes two-or-more spaces immediately before a
/// newline and emits <c>&lt;br /&gt;\n</c>.
/// </summary>
/// <remarks>
/// The trigger byte is <c>\n</c>: the inline-render dispatch hits a newline,
/// hands it here, and we walk backward over trailing spaces to decide whether
/// the line ends with a hard break. Firing on <c>\n</c> instead of every space
/// lets the outer dispatch use <c>SearchValues.IndexOfAny</c> to leap over
/// long prose runs between specials.
/// </remarks>
internal static class HardBreak
{
    /// <summary>Space byte.</summary>
    private const byte Sp = (byte)' ';

    /// <summary>Minimum trailing spaces required to form a hard break.</summary>
    private const int MinTrailingSpaces = 2;

    /// <summary>
    /// Handles a line feed at <paramref name="pos"/>; promotes it to <c>&lt;br /&gt;\n</c> when the immediately preceding bytes are 2+ spaces.
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor positioned at the <c>\n</c>; advanced past the <c>\n</c> on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when a hard break was emitted.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        var lfIndex = pos;
        var spaceStart = lfIndex;
        while (spaceStart > pendingTextStart && source[spaceStart - 1] == Sp)
        {
            spaceStart--;
        }

        if (lfIndex - spaceStart < MinTrailingSpaces)
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, spaceStart, writer);
        Utf8StringWriter.Write(writer, "<br />\n"u8);
        pos = lfIndex + 1;
        pendingTextStart = pos;
        return true;
    }
}
