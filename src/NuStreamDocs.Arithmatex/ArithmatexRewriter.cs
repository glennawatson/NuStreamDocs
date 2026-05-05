// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.Arithmatex;

/// <summary>
/// Stateless UTF-8 arithmatex rewriter. Walks the source byte
/// stream, skipping fenced and inline code, and wraps inline
/// <c>$x$</c> / block <c>$$x$$</c> math spans in the generic
/// pymdownx.arithmatex output shape.
/// </summary>
internal static class ArithmatexRewriter
{
    /// <summary>Width of the block-math <c>$$</c> marker.</summary>
    private const int BlockMarkerLength = 2;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        CodeAwareRewriter.Run(source, writer, TryRewriteMath);

    /// <summary>Tries to match a math span starting at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True when a math span was emitted.</returns>
    private static bool TryRewriteMath(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        if (source[offset] is not (byte)'$')
        {
            consumed = 0;
            return false;
        }

        return offset + 1 < source.Length && source[offset + 1] is (byte)'$'
            ? TryRewriteBlockMath(source, offset, writer, out consumed)
            : TryRewriteInlineMath(source, offset, writer, out consumed);
    }

    /// <summary>Tries to match a <c>$$x$$</c> block-math span.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor on the leading <c>$</c>.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryRewriteBlockMath(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        var contentStart = offset + BlockMarkerLength;
        var closeRel = source[contentStart..].IndexOf("$$"u8);
        if (closeRel < 0)
        {
            return false;
        }

        var contentEnd = contentStart + closeRel;
        if (contentEnd <= contentStart)
        {
            return false;
        }

        writer.Write("<div class=\"arithmatex\">\\["u8);
        writer.Write(source[contentStart..contentEnd]);
        writer.Write("\\]</div>"u8);
        consumed = contentEnd + BlockMarkerLength - offset;
        return true;
    }

    /// <summary>Tries to match a <c>$x$</c> inline-math span.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Cursor on the leading <c>$</c>.</param>
    /// <param name="writer">Sink.</param>
    /// <param name="consumed">Bytes consumed on success.</param>
    /// <returns>True on match.</returns>
    private static bool TryRewriteInlineMath(ReadOnlySpan<byte> source, int offset, IBufferWriter<byte> writer, out int consumed)
    {
        consumed = 0;
        var contentStart = offset + 1;
        if (contentStart >= source.Length || source[contentStart] is (byte)' ' or (byte)'\t' or (byte)'\n' or (byte)'$')
        {
            return false;
        }

        var closeOffset = FindInlineClose(source, contentStart);
        if (closeOffset < 0)
        {
            return false;
        }

        writer.Write("<span class=\"arithmatex\">\\("u8);
        writer.Write(source[contentStart..closeOffset]);
        writer.Write("\\)</span>"u8);
        consumed = closeOffset + 1 - offset;
        return true;
    }

    /// <summary>Scans for the matching closing <c>$</c> for an inline-math span.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="contentStart">Offset just past the opening <c>$</c>.</param>
    /// <returns>Offset of the closing <c>$</c>, or -1 if no valid close was found.</returns>
    private static int FindInlineClose(ReadOnlySpan<byte> source, int contentStart)
    {
        for (var p = contentStart; p < source.Length; p++)
        {
            switch (source[p])
            {
                case (byte)'\n':
                    return -1;
                case (byte)'$' when p > contentStart && IsValidInlineClose(source, p):
                    return p;
            }
        }

        return -1;
    }

    /// <summary>Returns true when the <c>$</c> at <paramref name="offset"/> is a valid inline-math close.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="offset">Position of the candidate <c>$</c>.</param>
    /// <returns>True when the close passes pymdownx's whitespace + digit-suffix rules.</returns>
    private static bool IsValidInlineClose(ReadOnlySpan<byte> source, int offset)
    {
        if (source[offset - 1] is (byte)' ' or (byte)'\t')
        {
            return false;
        }

        // Reject when the close-$ is followed by an ASCII digit (price-like input).
        return offset + 1 >= source.Length || source[offset + 1] is < (byte)'0' or > (byte)'9';
    }
}
