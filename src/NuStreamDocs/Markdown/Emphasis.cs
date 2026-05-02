// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

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

    /// <summary>Gets the UTF-8 bytes for the <c>em</c> open tag.</summary>
    private static ReadOnlySpan<byte> EmOpen => "<em>"u8;

    /// <summary>Gets the UTF-8 bytes for the <c>em</c> close tag.</summary>
    private static ReadOnlySpan<byte> EmClose => "</em>"u8;

    /// <summary>Gets the UTF-8 bytes for the <c>strong</c> open tag.</summary>
    private static ReadOnlySpan<byte> StrongOpen => "<strong>"u8;

    /// <summary>Gets the UTF-8 bytes for the <c>strong</c> close tag.</summary>
    private static ReadOnlySpan<byte> StrongClose => "</strong>"u8;

    /// <summary>Gets the UTF-8 bytes for the combined <c>strong</c>+<c>em</c> open.</summary>
    private static ReadOnlySpan<byte> StrongEmOpen => "<strong><em>"u8;

    /// <summary>Gets the UTF-8 bytes for the combined <c>em</c>+<c>strong</c> close.</summary>
    private static ReadOnlySpan<byte> StrongEmClose => "</em></strong>"u8;

    /// <summary>
    /// Handles an emphasis marker run at <paramref name="pos"/>.
    /// </summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the close run on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">The UTF-8 sink.</param>
    /// <returns>True when an emphasis run was emitted.</returns>
    public static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        var marker = source[pos];
        var openRun = AsciiByteHelpers.RunLength(source, pos, marker);
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

    /// <summary>Finds the start of a matching close run.</summary>
    /// <param name="source">The UTF-8 source.</param>
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
            var run = AsciiByteHelpers.RunLength(source, i, marker);
            if (run >= length)
            {
                return runStart;
            }

            i = runStart + run;
        }

        return -1;
    }

    /// <summary>Writes the open + content + close tags for the chosen <paramref name="openLength"/>.</summary>
    /// <param name="source">the UTF-8 source.</param>
    /// <param name="contentStart">Start of inner content.</param>
    /// <param name="closeStart">Start of close run.</param>
    /// <param name="openLength">1 = em, 2 = strong, 3 = strong+em.</param>
    /// <param name="writer">the UTF-8 sink.</param>
    private static void EmitWrapped(ReadOnlySpan<byte> source, int contentStart, int closeStart, int openLength, IBufferWriter<byte> writer)
    {
        Utf8StringWriter.Write(writer, OpenTag(openLength));
        InlineRenderer.Render(source[contentStart..closeStart], writer);
        Utf8StringWriter.Write(writer, CloseTag(openLength));
    }

    /// <summary>Returns the open the UTF-8 tag bytes for <paramref name="openLength"/>.</summary>
    /// <param name="openLength">1, 2, or 3.</param>
    /// <returns>Open tag bytes.</returns>
    private static ReadOnlySpan<byte> OpenTag(int openLength) => openLength switch
    {
        MaxRunLength => StrongEmOpen,
        StrongRun => StrongOpen,
        _ => EmOpen,
    };

    /// <summary>Returns the close the UTF-8 tag bytes for <paramref name="openLength"/>.</summary>
    /// <param name="openLength">1, 2, or 3.</param>
    /// <returns>Close tag bytes.</returns>
    private static ReadOnlySpan<byte> CloseTag(int openLength) => openLength switch
    {
        MaxRunLength => StrongEmClose,
        StrongRun => StrongClose,
        _ => EmClose,
    };

    /// <summary>Returns true when an underscore run at <paramref name="pos"/> is intra-word and should not delimit emphasis.</summary>
    /// <param name="source">the UTF-8 source.</param>
    /// <param name="pos">Position of the open marker.</param>
    /// <param name="contentStart">First byte of the inner content (just past the open run).</param>
    /// <param name="closeStart">Position of the close marker.</param>
    /// <param name="length">Run length being considered.</param>
    /// <returns>True when both the open and close violate the intra-word rule.</returns>
    private static bool IsIntraWord(ReadOnlySpan<byte> source, int pos, int contentStart, int closeStart, int length)
    {
        var openIntraWord = pos > 0
            && AsciiByteHelpers.IsAsciiIdentifierByte(source[pos - 1])
            && contentStart < source.Length
            && AsciiByteHelpers.IsAsciiIdentifierByte(source[contentStart]);

        var closeIntraWord = closeStart > 0
            && AsciiByteHelpers.IsAsciiIdentifierByte(source[closeStart - 1])
            && closeStart + length < source.Length
            && AsciiByteHelpers.IsAsciiIdentifierByte(source[closeStart + length]);

        // Reject when *either* end is intra-word — the run can't legally delimit on that side.
        return openIntraWord || closeIntraWord;
    }
}
