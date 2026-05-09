// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Span-based UTF-8 line splitting helpers. Covers <c>\r\n</c>, <c>\n</c>, and bare <c>\r</c>
/// terminators.
/// </summary>
public static class Utf8LineSpan
{
    /// <summary>Length of a <c>\r\n</c> line terminator in bytes.</summary>
    private const int CrLfLength = 2;

    /// <summary>
    /// Returns the index of the first line-terminator byte at or after <paramref name="cursor"/>,
    /// or <paramref name="source"/>.Length when none.
    /// </summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="cursor">Search-start offset.</param>
    /// <returns>Exclusive end of the current line content (before any <c>\r</c> or <c>\n</c>).</returns>
    public static int FindLineEnd(ReadOnlySpan<byte> source, int cursor)
    {
        var rest = source[cursor..];
        var hit = rest.IndexOfAny((byte)'\r', (byte)'\n');
        return hit < 0 ? source.Length : cursor + hit;
    }

    /// <summary>
    /// Advances past the line terminator at <paramref name="lineEnd"/>; consumes
    /// <c>\r\n</c>, <c>\n</c>, or <c>\r</c>.
    /// </summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="lineEnd">Index of the first line-terminator byte (or source length).</param>
    /// <returns>Index of the next line's first byte (or source length).</returns>
    public static int AdvancePastLineTerminator(ReadOnlySpan<byte> source, int lineEnd)
    {
        if (lineEnd >= source.Length)
        {
            return source.Length;
        }

        return source[lineEnd] is (byte)'\r' && lineEnd + 1 < source.Length && source[lineEnd + 1] is (byte)'\n'
            ? lineEnd + CrLfLength
            : lineEnd + 1;
    }
}
