// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using NuStreamDocs.Bibliography;

namespace NuStreamDocs.Benchmarks;

/// <summary>
/// Per-call cost of <see cref="LocatorLabel"/> after it moved from a 25-arm <c>when</c>-guarded
/// <c>switch</c> to a stack-buffer ASCII case-fold plus a
/// <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/> with a
/// <c>ReadOnlySpan&lt;byte&gt;</c> alternate lookup. The hit / miss split pins the recognized-label
/// path and the (longest, then dropped) unknown-label path the parser hits on free-form pinpoints.
/// </summary>
[ShortRunJob]
[MemoryDiagnoser]
public class LocatorLabelBenchmarks
{
    /// <summary>Recognized labels — a spread of lengths plus mixed-case spellings that exercise the case-fold.</summary>
    private static readonly byte[][] KnownLabels =
    [
        [.. "p"u8],
        [.. "pp"u8],
        [.. "page"u8],
        [.. "para"u8],
        [.. "paragraphs"u8],
        [.. "l"u8],
        [.. "Line"u8],
        [.. "ch"u8],
        [.. "Chapter"u8],
        [.. "s"u8],
        [.. "Section"u8],
        [.. "sch"u8],
        [.. "art"u8],
        [.. "Article"u8]
    ];

    /// <summary>Unrecognized labels — none classify, so each takes the fall-through path.</summary>
    private static readonly byte[][] UnknownLabels =
    [
        [.. "x"u8],
        [.. "pgs"u8],
        [.. "verse"u8],
        [.. "fol"u8],
        [.. "note"u8],
        [.. "appendix"u8],
        [.. "this-is-way-too-long"u8],
        [.. "n"u8]
    ];

    /// <summary>Classifies every recognized label.</summary>
    /// <returns>The summed <c>LocatorKind</c> ordinals (kept to defeat dead-code elimination).</returns>
    [Benchmark]
    public int ClassifyHits()
    {
        var total = 0;
        for (var i = 0; i < KnownLabels.Length; i++)
        {
            total += (int)LocatorLabel.Classify(KnownLabels[i]);
        }

        return total;
    }

    /// <summary>Classifies every unrecognized label (each falls through to <c>Other</c>).</summary>
    /// <returns>The summed <c>LocatorKind</c> ordinals (kept to defeat dead-code elimination).</returns>
    [Benchmark]
    public int ClassifyMisses()
    {
        var total = 0;
        for (var i = 0; i < UnknownLabels.Length; i++)
        {
            total += (int)LocatorLabel.Classify(UnknownLabels[i]);
        }

        return total;
    }
}
