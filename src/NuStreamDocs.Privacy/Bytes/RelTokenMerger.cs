// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Privacy.Bytes;

/// <summary>
/// Whitespace-separated token merger used by the anchor-hardening byte
/// pass to combine an existing <c>rel</c> attribute value with the
/// privacy-mandated tokens (<c>noopener noreferrer</c>) without
/// duplicating tokens that are already present.
/// </summary>
internal static class RelTokenMerger
{
    /// <summary>Maximum tokens we expect on either side; well above any real-world <c>rel</c> attribute.</summary>
    private const int MaxTokens = 16;

    /// <summary>Number of <see cref="int"/> slots per (start,end) range entry.</summary>
    private const int RangeStride = 2;

    /// <summary>Emits <paramref name="existing"/> followed by any tokens from <paramref name="extra"/> that aren't already present, normalising to single-space separators.</summary>
    /// <param name="existing">Current <c>rel</c> value (may contain irregular whitespace).</param>
    /// <param name="extra">Extra tokens to merge in.</param>
    /// <param name="sink">UTF-8 sink that receives the merged token list.</param>
    public static void MergeInto(ReadOnlySpan<byte> existing, ReadOnlySpan<byte> extra, IBufferWriter<byte> sink)
    {
        Span<int> existingRanges = stackalloc int[MaxTokens * RangeStride];
        var existingCount = IndexTokens(existing, existingRanges);

        Span<int> extraRanges = stackalloc int[MaxTokens * RangeStride];
        var extraCount = IndexTokens(extra, extraRanges);

        var emittedAny = EmitNormalizedTokens(existing, existingRanges, existingCount, sink, isFirst: true);

        for (var i = 0; i < extraCount; i++)
        {
            var token = TokenAt(extra, extraRanges, i);
            if (TokenInList(existing, existingRanges, existingCount, token))
            {
                continue;
            }

            if (TokenInList(extra, extraRanges, i, token))
            {
                continue;
            }

            EmitToken(token, sink, !emittedAny);
            emittedAny = true;
        }
    }

    /// <summary>Walks <paramref name="source"/> and records (start,end) byte offsets for each whitespace-separated token.</summary>
    /// <param name="source">Source span.</param>
    /// <param name="ranges">Destination buffer of (start, end) pairs.</param>
    /// <returns>The count of tokens written into <paramref name="ranges"/>.</returns>
    private static int IndexTokens(ReadOnlySpan<byte> source, Span<int> ranges)
    {
        var count = 0;
        var p = 0;
        while (p < source.Length && (count * RangeStride) + 1 < ranges.Length)
        {
            while (p < source.Length && ByteHelpers.IsAsciiWhitespace(source[p]))
            {
                p++;
            }

            if (p >= source.Length)
            {
                break;
            }

            var start = p;
            while (p < source.Length && !ByteHelpers.IsAsciiWhitespace(source[p]))
            {
                p++;
            }

            ranges[count * RangeStride] = start;
            ranges[(count * RangeStride) + 1] = p;
            count++;
        }

        return count;
    }

    /// <summary>Emits the tokens described by <paramref name="ranges"/> into <paramref name="sink"/> with single-space separators.</summary>
    /// <param name="source">Source span the ranges index into.</param>
    /// <param name="ranges">Token-range buffer.</param>
    /// <param name="count">Token count.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="isFirst">Whether the first token here is also the first overall (no leading space).</param>
    /// <returns>True when at least one token was emitted.</returns>
    private static bool EmitNormalizedTokens(ReadOnlySpan<byte> source, ReadOnlySpan<int> ranges, int count, IBufferWriter<byte> sink, bool isFirst)
    {
        for (var i = 0; i < count; i++)
        {
            EmitToken(TokenAt(source, ranges, i), sink, isFirst && i is 0);
        }

        return count > 0;
    }

    /// <summary>Returns the byte slice of the i-th token described by <paramref name="ranges"/>.</summary>
    /// <param name="source">Source span.</param>
    /// <param name="ranges">Token-range buffer.</param>
    /// <param name="i">Token index.</param>
    /// <returns>Token byte span.</returns>
    private static ReadOnlySpan<byte> TokenAt(ReadOnlySpan<byte> source, ReadOnlySpan<int> ranges, int i) =>
        source[ranges[i * RangeStride]..ranges[(i * RangeStride) + 1]];

    /// <summary>Returns true when <paramref name="candidate"/> matches any of the first <paramref name="count"/> tokens in <paramref name="ranges"/> (case-insensitive).</summary>
    /// <param name="source">Source span.</param>
    /// <param name="ranges">Token-range buffer.</param>
    /// <param name="count">Number of valid token entries to scan.</param>
    /// <param name="candidate">Candidate token bytes.</param>
    /// <returns>True when a duplicate already exists in the list.</returns>
    private static bool TokenInList(ReadOnlySpan<byte> source, ReadOnlySpan<int> ranges, int count, ReadOnlySpan<byte> candidate)
    {
        for (var i = 0; i < count; i++)
        {
            if (ByteHelpers.EqualsIgnoreAsciiCase(TokenAt(source, ranges, i), candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Writes <paramref name="token"/> into <paramref name="sink"/>, prepending a single-byte space when not the first emit.</summary>
    /// <param name="token">Token bytes.</param>
    /// <param name="sink">UTF-8 sink.</param>
    /// <param name="isFirst">Whether this is the first token overall.</param>
    private static void EmitToken(ReadOnlySpan<byte> token, IBufferWriter<byte> sink, bool isFirst)
    {
        var prefix = isFirst ? 0 : 1;
        var dst = sink.GetSpan(token.Length + prefix);
        if (!isFirst)
        {
            dst[0] = (byte)' ';
        }

        token.CopyTo(dst[prefix..]);
        sink.Advance(token.Length + prefix);
    }
}
