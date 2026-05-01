// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.Mark;

/// <summary>
/// Stateless UTF-8 mark rewriter. Replaces matched <c>==…==</c>
/// spans with <c>&lt;mark&gt;…&lt;/mark&gt;</c>, skipping fenced-code
/// regions and inline-code spans.
/// </summary>
internal static class MarkRewriter
{
    /// <summary>Width of the <c>==</c> marker on each side of a span.</summary>
    private const int MarkerLength = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i) && MarkdownCodeScanner.TryConsumeFence(source, i, out var fenceEnd))
            {
                writer.Write(source[i..fenceEnd]);
                i = fenceEnd;
                continue;
            }

            switch (source[i])
            {
                // Inline code: copy `…` runs through unchanged.
                case (byte)'`':
                    {
                        var inlineEnd = MarkdownCodeScanner.ConsumeInlineCode(source, i);
                        writer.Write(source[i..inlineEnd]);
                        i = inlineEnd;
                        continue;
                    }

                case (byte)'=' when i + 1 < source.Length && source[i + 1] == (byte)'='
                                                          && TryFindClose(source, i + MarkerLength, out var contentEnd):
                    {
                        writer.Write("<mark>"u8);
                        writer.Write(source[(i + MarkerLength)..contentEnd]);
                        writer.Write("</mark>"u8);
                        i = contentEnd + MarkerLength;
                        continue;
                    }
            }

            var dest = writer.GetSpan(1);
            dest[0] = source[i];
            writer.Advance(1);
            i++;
        }
    }

    /// <summary>Searches for the closing <c>==</c> on the same line.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="start">Offset just past the opening <c>==</c>.</param>
    /// <param name="contentEnd">Offset of the closing <c>==</c> on success.</param>
    /// <returns>True when a closing marker was found.</returns>
    private static bool TryFindClose(ReadOnlySpan<byte> source, int start, out int contentEnd)
    {
        contentEnd = 0;
        if (start >= source.Length || source[start] is (byte)' ' or (byte)'=' or (byte)'\n')
        {
            return false;
        }

        for (var p = start; p + 1 < source.Length; p++)
        {
            switch (source[p])
            {
                case (byte)'\n':
                    return false;
                case (byte)'=' when source[p + 1] == (byte)'=':
                    {
                        contentEnd = p;
                        return true;
                    }
            }
        }

        return false;
    }
}
