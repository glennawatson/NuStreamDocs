// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown.Common;

namespace NuStreamDocs.MarkdownExtensions.CheckList;

/// <summary>
/// Stateless UTF-8 check-list rewriter. Replaces the
/// <c>[?]&lt;space&gt;</c> token that follows a list bullet with
/// either a checked or unchecked disabled <c>&lt;input&gt;</c>
/// element.
/// </summary>
internal static class CheckListRewriter
{
    /// <summary>Length of the bullet plus its trailing space (<c>"- "</c>).</summary>
    private const int BulletLength = 2;

    /// <summary>Length of the <c>[?]&lt;space&gt;</c> task marker that follows the bullet.</summary>
    private const int MarkerLength = 4;

    /// <summary>Offset of the marker character (space or x) inside the <c>[?]</c> token.</summary>
    private const int MarkerCharOffset = 1;

    /// <summary>Offset of the closing <c>]</c> inside the <c>[?]</c> token.</summary>
    private const int CloseBracketOffset = 2;

    /// <summary>Offset of the trailing space that follows <c>[?]</c>.</summary>
    private const int TrailingSpaceOffset = 3;

    /// <summary>Gets the UTF-8 fragment for an unchecked checkbox.</summary>
    private static ReadOnlySpan<byte> Unchecked => "<input type=\"checkbox\" disabled>"u8;

    /// <summary>Gets the UTF-8 fragment for a checked checkbox.</summary>
    private static ReadOnlySpan<byte> Checked => "<input type=\"checkbox\" checked disabled>"u8;

    /// <summary>Rewrites <paramref name="source"/> into <paramref name="writer"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    public static void Rewrite(ReadOnlySpan<byte> source, IBufferWriter<byte> writer)
    {
        var i = 0;
        while (i < source.Length)
        {
            if (MarkdownCodeScanner.AtLineStart(source, i)
                && TryParseTaskMarker(source, i, out var afterIndent, out var bulletEnd, out var marker, out var afterMarker))
            {
                writer.Write(source[i..afterIndent]);
                writer.Write(source[afterIndent..bulletEnd]);
                writer.Write(marker is (byte)'x' or (byte)'X' ? Checked : Unchecked);
                var lineEnd = MarkdownCodeScanner.LineEnd(source, afterMarker);
                writer.Write(source[afterMarker..lineEnd]);
                i = lineEnd;
                continue;
            }

            var passEnd = MarkdownCodeScanner.LineEnd(source, i);
            writer.Write(source[i..passEnd]);
            i = passEnd;
        }
    }

    /// <summary>Tries to detect a task marker on the line at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Line-start offset.</param>
    /// <param name="afterIndent">Set to the offset just past leading spaces.</param>
    /// <param name="bulletEnd">Set to the offset just past the bullet plus its trailing space.</param>
    /// <param name="marker">Set to the byte inside <c>[…]</c> (space or x).</param>
    /// <param name="afterMarker">Set to the offset just past the marker plus the trailing space.</param>
    /// <returns>True when a task marker was found.</returns>
    private static bool TryParseTaskMarker(
        ReadOnlySpan<byte> source,
        int offset,
        out int afterIndent,
        out int bulletEnd,
        out byte marker,
        out int afterMarker)
    {
        bulletEnd = 0;
        marker = 0;
        afterMarker = 0;

        var p = SkipSpaces(source, offset);
        afterIndent = p;
        if (!TryConsumeBullet(source, ref p))
        {
            return false;
        }

        bulletEnd = p;
        if (!TryConsumeMarker(source, p, out marker))
        {
            return false;
        }

        afterMarker = p + MarkerLength;
        return true;
    }

    /// <summary>Advances past leading spaces.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Starting offset.</param>
    /// <returns>Offset of the first non-space byte.</returns>
    private static int SkipSpaces(ReadOnlySpan<byte> source, int offset)
    {
        var p = offset;
        while (p < source.Length && source[p] == (byte)' ')
        {
            p++;
        }

        return p;
    }

    /// <summary>Tries to consume a list bullet (<c>-</c>, <c>*</c>, or <c>+</c>) followed by a space.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="p">Cursor; advanced past the bullet and trailing space on success.</param>
    /// <returns>True when a bullet was consumed.</returns>
    private static bool TryConsumeBullet(ReadOnlySpan<byte> source, ref int p)
    {
        if (p >= source.Length || source[p] is not ((byte)'-' or (byte)'*' or (byte)'+'))
        {
            return false;
        }

        if (p + 1 >= source.Length || source[p + 1] != (byte)' ')
        {
            return false;
        }

        p += BulletLength;
        return true;
    }

    /// <summary>Tries to consume the <c>[?]&lt;space&gt;</c> task marker at <paramref name="offset"/>.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <param name="offset">Offset of the opening <c>[</c>.</param>
    /// <param name="marker">Set to the byte inside the brackets on success.</param>
    /// <returns>True when a valid marker was consumed.</returns>
    private static bool TryConsumeMarker(ReadOnlySpan<byte> source, int offset, out byte marker)
    {
        marker = 0;
        if (offset + MarkerLength > source.Length || source[offset] != (byte)'[')
        {
            return false;
        }

        marker = source[offset + MarkerCharOffset];
        return marker is (byte)' ' or (byte)'x' or (byte)'X'
            && source[offset + CloseBracketOffset] == (byte)']' && source[offset + TrailingSpaceOffset] == (byte)' ';
    }
}
