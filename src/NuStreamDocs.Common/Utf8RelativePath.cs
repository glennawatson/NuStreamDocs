// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Common;

/// <summary>
/// Byte-only helper that emits a forward-slashed UTF-8 URL pointing at one
/// docs-root-relative path, expressed relative to another such path's directory.
/// </summary>
public static class Utf8RelativePath
{
    /// <summary>UTF-8 byte for the segment separator.</summary>
    private const byte SegmentSeparator = (byte)'/';

    /// <summary>UTF-8 byte emitted when the source directory equals the target so the link still resolves to the current page.</summary>
    private const byte SelfReference = (byte)'.';

    /// <summary>Gets the UTF-8 bytes of the parent-directory segment <c>../</c> emitted once per unmatched source segment.</summary>
    private static ReadOnlySpan<byte> ParentSegment => "../"u8;

    /// <summary>Writes a forward-slashed UTF-8 URL for <paramref name="target"/> relative to <paramref name="from"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="from">Forward-slashed UTF-8 directory bytes that the relative URL is computed against; no leading or trailing slash.</param>
    /// <param name="target">Forward-slashed UTF-8 path bytes to point at; no leading slash.</param>
    /// <remarks>
    /// Walks both spans on segment boundaries to find their longest common prefix,
    /// then emits one <c>../</c> per unmatched <paramref name="from"/> segment
    /// followed by the unmatched suffix of <paramref name="target"/>. When
    /// <paramref name="from"/> equals <paramref name="target"/> a single <c>.</c>
    /// is emitted so the link still resolves to the current page.
    /// </remarks>
    public static void WriteRelative(IBufferWriter<byte> writer, ReadOnlySpan<byte> from, ReadOnlySpan<byte> target)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (from.IsEmpty && target.IsEmpty)
        {
            WriteSelfReference(writer);
            return;
        }

        if (from.IsEmpty)
        {
            writer.Write(target);
            return;
        }

        var sharedBoundary = FindSharedBoundary(from, target);
        var fromTail = from[sharedBoundary..];
        var targetTail = target[sharedBoundary..];

        // Strip any leading separator left over after the shared prefix so the segment count and suffix are clean.
        if (fromTail is [SegmentSeparator, ..])
        {
            fromTail = fromTail[1..];
        }

        if (targetTail is [SegmentSeparator, ..])
        {
            targetTail = targetTail[1..];
        }

        var parentCount = CountSegments(fromTail);
        if (parentCount is 0 && targetTail.IsEmpty)
        {
            WriteSelfReference(writer);
            return;
        }

        for (var i = 0; i < parentCount; i++)
        {
            writer.Write(ParentSegment);
        }

        if (targetTail.IsEmpty)
        {
            return;
        }

        writer.Write(targetTail);
    }

    /// <summary>Writes a single self-reference byte (<c>.</c>) into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    private static void WriteSelfReference(IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(1);
        dst[0] = SelfReference;
        writer.Advance(1);
    }

    /// <summary>Finds the longest byte prefix shared by <paramref name="from"/> and <paramref name="target"/> that ends at a clean segment boundary in both spans.</summary>
    /// <param name="from">Source-directory bytes.</param>
    /// <param name="target">Target path bytes.</param>
    /// <returns>Byte offset just past the last byte that lives in the shared prefix.</returns>
    private static int FindSharedBoundary(ReadOnlySpan<byte> from, ReadOnlySpan<byte> target)
    {
        var max = Math.Min(from.Length, target.Length);
        var lastBoundary = 0;
        var i = 0;
        while (i < max && from[i] == target[i])
        {
            i++;
            if (IsBoundary(from, i) && IsBoundary(target, i))
            {
                lastBoundary = i;
            }
        }

        return lastBoundary;
    }

    /// <summary>Returns true when <paramref name="offset"/> sits at a segment boundary in <paramref name="span"/> (end of span or just before a separator).</summary>
    /// <param name="span">Forward-slashed UTF-8 span.</param>
    /// <param name="offset">Byte offset to test.</param>
    /// <returns>True when the offset is a segment boundary.</returns>
    private static bool IsBoundary(ReadOnlySpan<byte> span, int offset) =>
        offset == span.Length || span[offset] == SegmentSeparator;

    /// <summary>Counts the slash-separated segments in <paramref name="span"/>; assumes no leading or trailing separator.</summary>
    /// <param name="span">Forward-slashed UTF-8 path span.</param>
    /// <returns>Segment count; zero when <paramref name="span"/> is empty.</returns>
    private static int CountSegments(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty)
        {
            return 0;
        }

        var count = 1;
        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] == SegmentSeparator)
            {
                count++;
            }
        }

        return count;
    }
}
