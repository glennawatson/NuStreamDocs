// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Emphasis / strong handler. Recognizes <c>*em*</c>, <c>**strong**</c>, <c>***both***</c> and the
/// underscore equivalents. An opening run must be followed by non-whitespace, a closing run must follow
/// non-whitespace, and underscore runs cannot open or close inside a word so <c>foo_bar</c>-shaped
/// identifiers stay literal. Nested emphasis inside the span is matched before the outer close is chosen.
/// </summary>
internal static class Emphasis
{
    /// <summary>Maximum emphasis-marker run length the probe considers (triple = strong + em).</summary>
    private const int MaxRunLength = 3;

    /// <summary>Strong-emphasis run length.</summary>
    private const int StrongRun = 2;

    /// <summary>Length of a backslash escape sequence.</summary>
    private const int EscapeLength = 2;

    /// <summary>Marker-run inspections one match attempt may spend before it gives up and leaves the text literal.</summary>
    private const int MaxSteps = 1024;

    /// <summary>Backslash byte.</summary>
    private const byte Backslash = (byte)'\\';

    /// <summary>Backtick byte.</summary>
    private const byte Backtick = (byte)'`';

    /// <summary>Underscore byte.</summary>
    private const byte Underscore = (byte)'_';

    /// <summary>First byte of a multi-byte UTF-8 sequence; such bytes count as word characters.</summary>
    private const byte FirstNonAsciiByte = 0x80;

    /// <summary>Bytes the close search must stop at: both emphasis markers, escapes and code spans.</summary>
    private static readonly SearchValues<byte> CloseSearchBytes = SearchValues.Create("*_\\`"u8);

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

    /// <summary>Handles an emphasis marker run at <paramref name="pos"/>.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="pos">Cursor; advanced past the close run on success.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">The UTF-8 sink.</param>
    /// <returns>True when an emphasis run was emitted.</returns>
    internal static bool TryHandle(
        ReadOnlySpan<byte> source,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer)
    {
        var steps = MaxSteps;
        if (!TryMatch(source, pos, ref steps, out var openStart, out var openLength, out var closeStart))
        {
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, openStart, writer);
        EmitWrapped(source, openStart + openLength, closeStart, openLength, writer);
        pos = closeStart + openLength;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Finds the emphasis span whose opening run starts at <paramref name="pos"/>.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="pos">Index of the first byte of the marker run.</param>
    /// <param name="steps">Remaining marker-run inspections.</param>
    /// <param name="openStart">Index of the first byte of the opening delimiter; earlier bytes of the run stay literal.</param>
    /// <param name="openLength">1 = em, 2 = strong, 3 = strong+em.</param>
    /// <param name="closeStart">Index of the first byte of the closing delimiter.</param>
    /// <returns>True when a span was found.</returns>
    private static bool TryMatch(
        ReadOnlySpan<byte> source,
        int pos,
        ref int steps,
        out int openStart,
        out int openLength,
        out int closeStart)
    {
        openStart = 0;
        openLength = 0;
        closeStart = -1;

        var marker = source[pos];
        var run = AsciiByteHelpers.RunLength(source, pos, marker);
        if (!CanOpen(source, pos, run, marker))
        {
            return false;
        }

        for (var probe = Math.Min(run, MaxRunLength); probe >= 1; probe--)
        {
            var candidate = FindClose(source, pos + run, marker, probe, ref steps);
            if (candidate < 0)
            {
                continue;
            }

            var surplus = run - probe;
            var outerClose = surplus > 0 ? FindClose(source, candidate + probe, marker, surplus, ref steps) : -1;
            if (outerClose >= 0)
            {
                openStart = pos;
                openLength = surplus;
                closeStart = outerClose;
                return true;
            }

            if (openLength is 0)
            {
                openStart = pos + surplus;
                openLength = probe;
                closeStart = candidate;
            }

            if (surplus is 0)
            {
                break;
            }
        }

        return openLength > 0;
    }

    /// <summary>Finds the start of the run that closes an opener of <paramref name="length"/> markers.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="contentStart">First byte after the opening run.</param>
    /// <param name="marker">Marker byte.</param>
    /// <param name="length">Required close run length.</param>
    /// <param name="steps">Remaining marker-run inspections.</param>
    /// <returns>Start index of the close run, or -1.</returns>
    private static int FindClose(ReadOnlySpan<byte> source, int contentStart, byte marker, int length, ref int steps)
    {
        var i = contentStart;
        while (TryAdvanceToSpecial(source, ref i, ref steps))
        {
            if (source[i] != marker)
            {
                i = SkipNonMarker(source, i);
                continue;
            }

            var run = AsciiByteHelpers.RunLength(source, i, marker);
            if (i > contentStart && run >= length && CanClose(source, i, run, marker))
            {
                return i;
            }

            i = SkipOrMatchNested(source, i, run, marker, ref steps);
        }

        return -1;
    }

    /// <summary>Moves <paramref name="index"/> to the next byte the close search must inspect.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Search cursor; advanced on success.</param>
    /// <param name="steps">Remaining marker-run inspections; one is spent per call.</param>
    /// <returns>True when a special byte was found within budget.</returns>
    private static bool TryAdvanceToSpecial(ReadOnlySpan<byte> source, ref int index, ref int steps)
    {
        if (index >= source.Length)
        {
            return false;
        }

        var rel = source[index..].IndexOfAny(CloseSearchBytes);
        if (rel < 0)
        {
            return false;
        }

        steps--;
        index += rel;
        return steps >= 0;
    }

    /// <summary>Returns the index past an escape, code span, or run of the other emphasis marker starting at <paramref name="index"/>.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Index of a backslash, backtick, or the emphasis marker that is not being matched.</param>
    /// <returns>Index of the first byte to inspect next.</returns>
    private static int SkipNonMarker(ReadOnlySpan<byte> source, int index)
    {
        var b = source[index];
        if (b is Backslash)
        {
            return index + EscapeLength;
        }

        var run = AsciiByteHelpers.RunLength(source, index, b);
        if (b is not Backtick)
        {
            return index + run;
        }

        var close = CodeSpan.FindMatchingClose(source, index + run, run);
        return close < 0 ? index + run : close + run;
    }

    /// <summary>Returns the index past a nested emphasis span opened by the run at <paramref name="index"/>, or past the run when none opens.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Index of the marker run.</param>
    /// <param name="run">Length of the run.</param>
    /// <param name="marker">Marker byte.</param>
    /// <param name="steps">Remaining marker-run inspections.</param>
    /// <returns>Index of the first byte to inspect next.</returns>
    private static int SkipOrMatchNested(ReadOnlySpan<byte> source, int index, int run, byte marker, ref int steps) =>
        CanOpen(source, index, run, marker) && TryMatch(source, index, ref steps, out _, out var length, out var close)
            ? close + length
            : index + run;

    /// <summary>True when the marker run at <paramref name="pos"/> can start emphasis.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="pos">Index of the run's first byte.</param>
    /// <param name="run">Length of the run.</param>
    /// <param name="marker">Marker byte.</param>
    /// <returns>True when non-whitespace follows the run and, for underscores, no word character precedes it.</returns>
    private static bool CanOpen(ReadOnlySpan<byte> source, int pos, int run, byte marker)
    {
        var after = pos + run;
        if (after >= source.Length || AsciiByteHelpers.IsAsciiWhitespace(source[after]))
        {
            return false;
        }

        return marker is not Underscore || pos is 0 || !IsWordByte(source[pos - 1]);
    }

    /// <summary>True when the marker run at <paramref name="pos"/> can end emphasis.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="pos">Index of the run's first byte.</param>
    /// <param name="run">Length of the run.</param>
    /// <param name="marker">Marker byte.</param>
    /// <returns>True when non-whitespace precedes the run and, for underscores, no word character follows it.</returns>
    private static bool CanClose(ReadOnlySpan<byte> source, int pos, int run, byte marker)
    {
        if (pos is 0 || AsciiByteHelpers.IsAsciiWhitespace(source[pos - 1]))
        {
            return false;
        }

        var after = pos + run;
        return marker is not Underscore || after >= source.Length || !IsWordByte(source[after]);
    }

    /// <summary>True for ASCII letters and digits and for any byte of a multi-byte UTF-8 sequence.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns>True when the byte is part of a word.</returns>
    private static bool IsWordByte(byte b) =>
        b >= FirstNonAsciiByte || AsciiByteHelpers.IsAsciiLetter(b) || AsciiByteHelpers.IsAsciiDigit(b);

    /// <summary>Writes the open + content + close tags for the chosen <paramref name="openLength"/>.</summary>
    /// <param name="source">the UTF-8 source.</param>
    /// <param name="contentStart">Start of inner content.</param>
    /// <param name="closeStart">Start of close run.</param>
    /// <param name="openLength">1 = em, 2 = strong, 3 = strong+em.</param>
    /// <param name="writer">the UTF-8 sink.</param>
    private static void EmitWrapped(
        ReadOnlySpan<byte> source,
        int contentStart,
        int closeStart,
        int openLength,
        IBufferWriter<byte> writer)
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
        _ => EmOpen
    };

    /// <summary>Returns the close the UTF-8 tag bytes for <paramref name="openLength"/>.</summary>
    /// <param name="openLength">1, 2, or 3.</param>
    /// <returns>Close tag bytes.</returns>
    private static ReadOnlySpan<byte> CloseTag(int openLength) => openLength switch
    {
        MaxRunLength => StrongEmClose,
        StrongRun => StrongClose,
        _ => EmClose
    };
}
