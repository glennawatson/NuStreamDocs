// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Greedy emphasis / strong handler.
/// </summary>
/// <remarks>
/// Recognizes <c>*em*</c>, <c>**strong**</c>, and the triple
/// <c>***both***</c> shape (and the underscore equivalents) by
/// probing the longest legal form first and falling back when no
/// matching close run is available. Underscore emphasis honors
/// CommonMark's intra-word rule: a <c>_</c> open or close
/// surrounded by word bytes on both sides is rejected, so
/// identifiers like <c>foo_bar_baz</c> never trigger emphasis.
/// </remarks>
internal static class Emphasis
{
    /// <summary>Maximum emphasis-marker run length the probe considers (triple = strong + em).</summary>
    private const int MaxRunLength = 3;

    /// <summary>Strong-emphasis run length.</summary>
    private const int StrongRun = 2;

    /// <summary>UTF-8 bytes for the <c>em</c> open tag.</summary>
    private static readonly byte[] EmOpen = [.. "<em>"u8];

    /// <summary>UTF-8 bytes for the <c>em</c> close tag.</summary>
    private static readonly byte[] EmClose = [.. "</em>"u8];

    /// <summary>UTF-8 bytes for the <c>strong</c> open tag.</summary>
    private static readonly byte[] StrongOpen = [.. "<strong>"u8];

    /// <summary>UTF-8 bytes for the <c>strong</c> close tag.</summary>
    private static readonly byte[] StrongClose = [.. "</strong>"u8];

    /// <summary>UTF-8 bytes for the combined <c>strong</c>+<c>em</c> open.</summary>
    private static readonly byte[] StrongEmOpen = [.. "<strong><em>"u8];

    /// <summary>UTF-8 bytes for the combined <c>em</c>+<c>strong</c> close.</summary>
    private static readonly byte[] StrongEmClose = [.. "</em></strong>"u8];

    /// <summary>
    /// Handles an emphasis marker run at <paramref name="pos"/>.
    /// </summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the close run on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">UTF-8 sink.</param>
    /// <returns>True when an emphasis run was emitted.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        var marker = source[pos];
        var openRun = MarkerRun(source, pos, marker);
        var maxProbe = openRun >= MaxRunLength ? MaxRunLength : openRun;

        var openLength = 0;
        var closeStart = -1;
        for (var probe = maxProbe; probe >= 1; probe--)
        {
            var contentStart = pos + probe;
            var candidate = FindClose(source, contentStart, marker, probe);
            if (candidate < 0)
            {
                continue;
            }

            if (marker is (byte)'_' && IsIntraWord(source, pos, contentStart, candidate, probe))
            {
                continue;
            }

            openLength = probe;
            closeStart = candidate;
            break;
        }

        if (openLength is 0)
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, pos, writer);
        EmitWrapped(source, pos + openLength, closeStart, openLength, writer);
        pos = closeStart + openLength;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Counts the run of <paramref name="marker"/> bytes starting at <paramref name="pos"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Run start.</param>
    /// <param name="marker">Marker byte.</param>
    /// <returns>Run length.</returns>
    public static int MarkerRun(ReadOnlySpan<byte> source, int pos, byte marker)
    {
        var i = pos;
        while (i < source.Length && source[i] == marker)
        {
            i++;
        }

        return i - pos;
    }

    /// <summary>Finds the start of a matching close run.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="searchFrom">First byte to consider.</param>
    /// <param name="marker">Marker byte.</param>
    /// <param name="length">Required close run length.</param>
    /// <returns>Start index of the close run, or -1.</returns>
    public static int FindClose(ReadOnlySpan<byte> source, int searchFrom, byte marker, int length)
    {
        var i = searchFrom;
        while (i < source.Length)
        {
            if (source[i] != marker)
            {
                i++;
                continue;
            }

            var runStart = i;
            var run = MarkerRun(source, i, marker);
            if (run >= length)
            {
                return runStart;
            }

            i = runStart + run;
        }

        return -1;
    }

    /// <summary>Writes the open + content + close tags for the chosen <paramref name="openLength"/>.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="contentStart">Start of inner content.</param>
    /// <param name="closeStart">Start of close run.</param>
    /// <param name="openLength">1 = em, 2 = strong, 3 = strong+em.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void EmitWrapped(ReadOnlySpan<byte> source, int contentStart, int closeStart, int openLength, IBufferWriter<byte> writer)
    {
        var (open, close) = TagsFor(openLength);
        Write(open, writer);
        InlineRenderer.Render(source[contentStart..closeStart], writer);
        Write(close, writer);
    }

    /// <summary>Returns the open + close UTF-8 tag bytes for <paramref name="openLength"/>.</summary>
    /// <param name="openLength">1, 2, or 3.</param>
    /// <returns>The open + close tag pair.</returns>
    private static (byte[] Open, byte[] Close) TagsFor(int openLength) => openLength switch
    {
        MaxRunLength => (StrongEmOpen, StrongEmClose),
        StrongRun => (StrongOpen, StrongClose),
        _ => (EmOpen, EmClose),
    };

    /// <summary>Returns true when an underscore run at <paramref name="pos"/> is intra-word and should not delimit emphasis.</summary>
    /// <param name="source">UTF-8 source.</param>
    /// <param name="pos">Position of the open marker.</param>
    /// <param name="contentStart">First byte of the inner content (just past the open run).</param>
    /// <param name="closeStart">Position of the close marker.</param>
    /// <param name="length">Run length being considered.</param>
    /// <returns>True when both the open and close violate the intra-word rule.</returns>
    private static bool IsIntraWord(ReadOnlySpan<byte> source, int pos, int contentStart, int closeStart, int length)
    {
        var openIntraWord = pos > 0
            && IsWordByte(source[pos - 1])
            && contentStart < source.Length
            && IsWordByte(source[contentStart]);

        var closeIntraWord = closeStart > 0
            && IsWordByte(source[closeStart - 1])
            && closeStart + length < source.Length
            && IsWordByte(source[closeStart + length]);

        // Reject when *either* end is intra-word — the run can't legally delimit on that side.
        return openIntraWord || closeIntraWord;
    }

    /// <summary>Returns true when <paramref name="b"/> is an ASCII identifier byte.</summary>
    /// <param name="b">UTF-8 byte.</param>
    /// <returns>True for letters, digits, or <c>_</c>.</returns>
    private static bool IsWordByte(byte b) =>
        b is >= (byte)'0' and <= (byte)'9'
          or >= (byte)'A' and <= (byte)'Z'
          or >= (byte)'a' and <= (byte)'z'
          or (byte)'_';

    /// <summary>Bulk-writes <paramref name="bytes"/>.</summary>
    /// <param name="bytes">UTF-8 bytes.</param>
    /// <param name="writer">UTF-8 sink.</param>
    private static void Write(ReadOnlySpan<byte> bytes, IBufferWriter<byte> writer)
    {
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }
}
