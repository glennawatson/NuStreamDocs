// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Pairs emphasis marker runs by the CommonMark delimiter-run rules: a closing run pairs with the nearest
/// opening run of the same marker, two markers are used when both runs have two or more left and one otherwise,
/// and a run that can both open and close does not pair across lengths that add up to a multiple of three.
/// </summary>
internal static class EmphasisMatcher
{
    /// <summary>Divisor of the rule that keeps runs able to both open and close from pairing when their lengths add up to a multiple of it.</summary>
    private const int RuleOfThree = 3;

    /// <summary>Marker count that makes a pair strong emphasis.</summary>
    private const int StrongLength = 2;

    /// <summary>Length of a backslash escape sequence.</summary>
    private const int EscapeLength = 2;

    /// <summary>Number of closer kinds: marker, whether the closer can open, and its length modulo three.</summary>
    private const int CloserKindCount = 12;

    /// <summary>Offset of the underscore closer kinds.</summary>
    private const int UnderscoreKindOffset = 6;

    /// <summary>Offset of the closer kinds that can also open.</summary>
    private const int CanOpenKindOffset = 3;

    /// <summary>Backslash byte.</summary>
    private const byte Backslash = (byte)'\\';

    /// <summary>Backtick byte.</summary>
    private const byte Backtick = (byte)'`';

    /// <summary>Underscore byte.</summary>
    private const byte Underscore = (byte)'_';

    /// <summary>Open-bracket byte that starts a link.</summary>
    private const byte OpenBracket = (byte)'[';

    /// <summary>Bang byte that starts an image.</summary>
    private const byte Bang = (byte)'!';

    /// <summary>Less-than byte that starts an autolink or raw HTML.</summary>
    private const byte LessThan = (byte)'<';

    /// <summary>Bytes the scan must stop at: both emphasis markers, escapes, code spans, links, images, autolinks and raw HTML.</summary>
    private static readonly SearchValues<byte> ScanBytes = SearchValues.Create("*_\\`[!<"u8);

    /// <summary>Records every marker run in <paramref name="source"/> and the pairs they form.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="table">Empty table with room for every run and pair of <paramref name="source"/>.</param>
    internal static void Build(ReadOnlySpan<byte> source, ref EmphasisTable table)
    {
        Span<int> stackFloors = stackalloc int[CloserKindCount];
        var index = 0;
        while (index < source.Length)
        {
            var rel = source[index..].IndexOfAny(ScanBytes);
            if (rel < 0)
            {
                return;
            }

            index += rel;
            index = source[index] switch
            {
                Backslash => index + EscapeLength,
                Backtick => SkipCodeSpan(source, index),
                OpenBracket => SkipLink(source, index),
                Bang => SkipImage(source, index),
                LessThan => SkipAngleConstruct(source, index),
                _ => AddRun(source, index, stackFloors, ref table)
            };
        }
    }

    /// <summary>Returns the index past the inline link at <paramref name="index"/>, or past the bracket when none starts there.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Index of the open bracket.</param>
    /// <returns>Index of the first byte to inspect next.</returns>
    private static int SkipLink(ReadOnlySpan<byte> source, int index) =>
        LinkSpan.TryReadShape(source, index, out var shape) ? shape.End : index + 1;

    /// <summary>Returns the index past the inline image at <paramref name="index"/>, or past the bang when none starts there.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Index of the bang.</param>
    /// <returns>Index of the first byte to inspect next.</returns>
    private static int SkipImage(ReadOnlySpan<byte> source, int index) =>
        index + 1 < source.Length && LinkSpan.TryReadShape(source, index + 1, out var shape) ? shape.End : index + 1;

    /// <summary>Returns the index past the autolink or raw HTML construct at <paramref name="index"/>, or past the angle bracket when none starts there.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Index of the less-than sign.</param>
    /// <returns>Index of the first byte to inspect next.</returns>
    private static int SkipAngleConstruct(ReadOnlySpan<byte> source, int index)
    {
        var end = AutoLink.FindEnd(source, index);
        if (end < 0)
        {
            end = RawHtml.FindEnd(source, index);
        }

        return end < 0 ? index + 1 : end;
    }

    /// <summary>Returns the index past the code span or unmatched backtick run at <paramref name="index"/>.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="index">Index of the first backtick.</param>
    /// <returns>Index of the first byte to inspect next.</returns>
    private static int SkipCodeSpan(ReadOnlySpan<byte> source, int index)
    {
        var run = AsciiByteHelpers.RunLength(source, index, Backtick);
        var close = CodeSpan.FindMatchingClose(source, index + run, run);
        return close < 0 ? index + run : close + run;
    }

    /// <summary>Records the marker run at <paramref name="start"/> and pairs it with earlier openers.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="start">Index of the run's first byte.</param>
    /// <param name="stackFloors">Per closer kind, the stack depth below which no opener can pair.</param>
    /// <param name="table">The table being built.</param>
    /// <returns>Index of the first byte after the run.</returns>
    private static int AddRun(ReadOnlySpan<byte> source, int start, scoped Span<int> stackFloors, ref EmphasisTable table)
    {
        var marker = source[start];
        var length = AsciiByteHelpers.RunLength(source, start, marker);
        var (canOpen, canClose) = EmphasisFlanking.Classify(source, start, length, marker);
        if (!canOpen && !canClose)
        {
            return start + length;
        }

        var index = table.AddRun(new(start, length, marker is Underscore, canOpen, canClose));
        if (canClose)
        {
            PairCloser(index, stackFloors, ref table);
        }

        if (canOpen && table.Runs[index].Remaining > 0)
        {
            table.Push(index);
        }

        return start + length;
    }

    /// <summary>Pairs the leading markers of a closing run with the nearest openers below it on the stack.</summary>
    /// <param name="closerIndex">Index of the closing run.</param>
    /// <param name="stackFloors">Per closer kind, the stack depth below which no opener can pair.</param>
    /// <param name="table">The table being built.</param>
    private static void PairCloser(int closerIndex, scoped Span<int> stackFloors, ref EmphasisTable table)
    {
        ref var closer = ref table.Runs[closerIndex];
        var kind = (closer.Underscore ? UnderscoreKindOffset : 0) + (closer.CanOpen ? CanOpenKindOffset : 0) + (closer.Length % RuleOfThree);
        while (closer.Remaining > 0)
        {
            var slot = FindOpener(closer.Underscore, closer.CanOpen, closer.Length, stackFloors[kind], ref table);
            if (slot < 0)
            {
                stackFloors[kind] = table.StackCount;
                return;
            }

            ref var opener = ref table.Runs[table.Stack[slot]];
            var length = closer.Remaining >= StrongLength && opener.Remaining >= StrongLength ? StrongLength : 1;
            opener.FirstPair = table.AddPair(new(opener.End - opener.OpenerUsed - length, closer.Start + closer.CloserUsed, length, opener.FirstPair));
            opener.OpenerUsed += length;
            closer.CloserUsed += length;

            table.StackCount = opener.Remaining > 0 ? slot + 1 : slot;
            LowerFloors(stackFloors, table.StackCount);
        }
    }

    /// <summary>Finds the nearest stack entry that can pair with a closing run.</summary>
    /// <param name="underscore">True when the closing run is underscores.</param>
    /// <param name="closerCanOpen">True when the closing run can also open emphasis.</param>
    /// <param name="closerLength">Length of the closing run.</param>
    /// <param name="floor">Stack depth below which no opener can pair.</param>
    /// <param name="table">The table being built.</param>
    /// <returns>Stack slot of the opener, or -1.</returns>
    /// <remarks>
    /// A run that can both open and close does not pair with a run when the two lengths add up to a multiple of
    /// three, unless the closing run's length is itself a multiple of three.
    /// </remarks>
    private static int FindOpener(bool underscore, bool closerCanOpen, int closerLength, int floor, ref EmphasisTable table)
    {
        var closerMultipleOfThree = closerLength % RuleOfThree is 0;
        for (var slot = table.StackCount - 1; slot >= floor; slot--)
        {
            ref var opener = ref table.Runs[table.Stack[slot]];
            if (opener.Underscore != underscore)
            {
                continue;
            }

            var oddMatch = (closerCanOpen || opener.CanClose) && !closerMultipleOfThree && (opener.Length + closerLength) % RuleOfThree is 0;
            if (!oddMatch)
            {
                return slot;
            }
        }

        return -1;
    }

    /// <summary>Keeps every floor within the stack after it shrank.</summary>
    /// <param name="stackFloors">Per closer kind, the stack depth below which no opener can pair.</param>
    /// <param name="stackCount">Current stack depth.</param>
    private static void LowerFloors(Span<int> stackFloors, int stackCount)
    {
        for (var i = 0; i < stackFloors.Length; i++)
        {
            stackFloors[i] = Math.Min(stackFloors[i], stackCount);
        }
    }
}
