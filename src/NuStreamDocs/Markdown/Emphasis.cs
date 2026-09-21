// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Markdown;

/// <summary>
/// Emphasis / strong handler. Recognizes <c>*em*</c>, <c>**strong**</c>, <c>***both***</c> and the
/// underscore equivalents by the CommonMark delimiter-run rules: a closing run pairs with the nearest opening
/// run, runs of different lengths pair, and underscore runs cannot open or close inside a word so
/// <c>foo_bar</c>-shaped identifiers stay literal.
/// </summary>
internal static class Emphasis
{
    /// <summary>Marker count that makes a pair strong emphasis.</summary>
    private const int StrongLength = 2;

    /// <summary>Marker bytes a source may hold and still keep its delimiter table on the stack.</summary>
    private const int StackMarkerLimit = 32;

    /// <summary>Emphasis spans that may be open around one position; deeper pairs stay literal.</summary>
    private const int MaxNestingDepth = 32;

    /// <summary>Gets the UTF-8 bytes for the <c>em</c> open tag.</summary>
    private static ReadOnlySpan<byte> EmOpen => "<em>"u8;

    /// <summary>Gets the UTF-8 bytes for the <c>em</c> close tag.</summary>
    private static ReadOnlySpan<byte> EmClose => "</em>"u8;

    /// <summary>Gets the UTF-8 bytes for the <c>strong</c> open tag.</summary>
    private static ReadOnlySpan<byte> StrongOpen => "<strong>"u8;

    /// <summary>Gets the UTF-8 bytes for the <c>strong</c> close tag.</summary>
    private static ReadOnlySpan<byte> StrongClose => "</strong>"u8;

    /// <summary>Renders <paramref name="source"/>, which holds at least one emphasis marker byte, pairing its emphasis first.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="markerCount">Number of <c>*</c> and <c>_</c> bytes in <paramref name="source"/>.</param>
    /// <param name="linksBlocked">True when inline links stay literal until the first inline element of <paramref name="source"/>.</param>
    /// <param name="writer">The UTF-8 sink.</param>
    internal static void Render(ReadOnlySpan<byte> source, int markerCount, bool linksBlocked, IBufferWriter<byte> writer)
    {
        if (markerCount <= StackMarkerLimit)
        {
            RenderWithStackTable(source, linksBlocked, writer);
            return;
        }

        RenderWithPooledTable(source, markerCount, linksBlocked, writer);
    }

    /// <summary>Handles an emphasis marker run at <paramref name="pos"/>.</summary>
    /// <param name="source">The UTF-8 source of the range being rendered.</param>
    /// <param name="origin">Position of <paramref name="source"/> within the source the table was built from.</param>
    /// <param name="pos">Cursor; advanced past the closing delimiter on success, and to the last byte of the marker run when the run stays literal.</param>
    /// <param name="pendingTextStart">Start of pending text run.</param>
    /// <param name="writer">The UTF-8 sink.</param>
    /// <param name="table">Delimiter table of the whole source.</param>
    /// <returns>True when an emphasis span was emitted.</returns>
    internal static bool TryHandle(
        ReadOnlySpan<byte> source,
        int origin,
        ref int pos,
        ref int pendingTextStart,
        IBufferWriter<byte> writer,
        ref EmphasisTable table)
    {
        var position = origin + pos;
        var runIndex = table.FindRun(position);
        if (runIndex < 0)
        {
            pos += AsciiByteHelpers.RunLength(source, pos, source[pos]) - 1;
            return false;
        }

        ref var run = ref table.Runs[runIndex];
        var pairIndex = run.FirstPair;
        while (pairIndex > 0 && table.Pairs[pairIndex - 1].OpenStart < position)
        {
            pairIndex = table.Pairs[pairIndex - 1].Next;
        }

        if (pairIndex is 0)
        {
            run.FirstPair = 0;
            pos += run.End - position - 1;
            return false;
        }

        var pair = table.Pairs[pairIndex - 1];
        run.FirstPair = pair.Next;
        if (table.Depth >= MaxNestingDepth || pair.CloseStart + pair.Length > origin + source.Length)
        {
            pos = pair.OpenStart + pair.Length - 1 - origin;
            return false;
        }

        InlineRenderer.FlushText(source, pendingTextStart, pair.OpenStart - origin, writer);
        var contentStart = pair.OpenStart + pair.Length;
        Utf8StringWriter.Write(writer, pair.Length is StrongLength ? StrongOpen : EmOpen);
        table.LinksBlocked = false;
        table.Depth++;
        InlineRenderer.RenderRange(source[(contentStart - origin)..(pair.CloseStart - origin)], contentStart, writer, ref table);
        table.Depth--;
        Utf8StringWriter.Write(writer, pair.Length is StrongLength ? StrongClose : EmClose);

        pos = pair.CloseStart + pair.Length - origin;
        pendingTextStart = pos;
        return true;
    }

    /// <summary>Renders <paramref name="source"/> with a delimiter table held on the stack.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="linksBlocked">True when inline links stay literal until the first inline element of <paramref name="source"/>.</param>
    /// <param name="writer">The UTF-8 sink.</param>
    private static void RenderWithStackTable(ReadOnlySpan<byte> source, bool linksBlocked, IBufferWriter<byte> writer)
    {
        Span<EmphasisRun> runs = stackalloc EmphasisRun[StackMarkerLimit];
        Span<EmphasisPair> pairs = stackalloc EmphasisPair[StackMarkerLimit];
        Span<int> stack = stackalloc int[StackMarkerLimit];
        RenderWithTable(source, new(runs, pairs, stack), linksBlocked, writer);
    }

    /// <summary>Renders <paramref name="source"/> with a delimiter table held in pooled arrays.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="markerCount">Number of <c>*</c> and <c>_</c> bytes in <paramref name="source"/>.</param>
    /// <param name="linksBlocked">True when inline links stay literal until the first inline element of <paramref name="source"/>.</param>
    /// <param name="writer">The UTF-8 sink.</param>
    private static void RenderWithPooledTable(ReadOnlySpan<byte> source, int markerCount, bool linksBlocked, IBufferWriter<byte> writer)
    {
        var runs = ArrayPool<EmphasisRun>.Shared.Rent(markerCount);
        var pairs = ArrayPool<EmphasisPair>.Shared.Rent(markerCount / StrongLength);
        var stack = ArrayPool<int>.Shared.Rent(markerCount);
        try
        {
            RenderWithTable(source, new(runs, pairs, stack), linksBlocked, writer);
        }
        finally
        {
            ArrayPool<EmphasisRun>.Shared.Return(runs);
            ArrayPool<EmphasisPair>.Shared.Return(pairs);
            ArrayPool<int>.Shared.Return(stack);
        }
    }

    /// <summary>Builds the delimiter table of <paramref name="source"/> and renders it.</summary>
    /// <param name="source">The UTF-8 source.</param>
    /// <param name="table">Empty table with room for every run and pair of <paramref name="source"/>.</param>
    /// <param name="linksBlocked">True when inline links stay literal until the first inline element of <paramref name="source"/>.</param>
    /// <param name="writer">The UTF-8 sink.</param>
    private static void RenderWithTable(ReadOnlySpan<byte> source, EmphasisTable table, bool linksBlocked, IBufferWriter<byte> writer)
    {
        table.LinksBlocked = linksBlocked;
        EmphasisMatcher.Build(source, ref table);
        InlineRenderer.RenderRange(source, 0, writer, ref table);
    }
}
