// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Html;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Backslash-escape handler for the inline pass.
/// </summary>
internal static class InlineEscape
{
    /// <summary>Length of one backslash-escape sequence (the backslash and one escaped byte).</summary>
    private const int EscapeSequenceLength = 2;

    /// <summary>
    /// Handles a backslash at <paramref name="pos"/>; emits the
    /// following byte verbatim (with HTML escaping where needed).
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the escape on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when the byte was a valid escape sequence.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        if (pos + 1 >= source.Length)
        {
            return false;
        }

        var next = source[pos + 1];
        if (!IsAsciiPunct(next))
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, pos, writer);
        HtmlEscape.EscapeText(source.Slice(pos + 1, 1), writer);
        pos += EscapeSequenceLength;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>True for ASCII punctuation per CommonMark §2.4.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True when the byte is a punctuation character.</returns>
    private static bool IsAsciiPunct(byte b) =>
        b is >= (byte)'!' and <= (byte)'/'
            or >= (byte)':' and <= (byte)'@'
            or >= (byte)'[' and <= (byte)'`'
            or >= (byte)'{' and <= (byte)'~';
}
