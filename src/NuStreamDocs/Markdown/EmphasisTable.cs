// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown;

/// <summary>
/// The delimiter runs of one inline source and the emphasis pairs formed from them. Capacity is fixed by the
/// caller: no more runs than marker bytes, no more pairs than half the marker bytes.
/// </summary>
internal ref struct EmphasisTable
{
    /// <summary>Initializes a new instance of the <see cref="EmphasisTable"/> struct.</summary>
    /// <param name="runs">Storage for the runs.</param>
    /// <param name="pairs">Storage for the pairs.</param>
    /// <param name="stack">Storage for the stack of runs still able to open emphasis.</param>
    internal EmphasisTable(Span<EmphasisRun> runs, Span<EmphasisPair> pairs, Span<int> stack)
    {
        Runs = runs;
        Pairs = pairs;
        Stack = stack;
        RunCount = 0;
        PairCount = 0;
        StackCount = 0;
        Cursor = 0;
        Depth = 0;
        LinksBlocked = false;
    }

    /// <summary>Gets the run storage.</summary>
    internal readonly Span<EmphasisRun> Runs { get; }

    /// <summary>Gets the pair storage.</summary>
    internal readonly Span<EmphasisPair> Pairs { get; }

    /// <summary>Gets the storage of the stack of run indexes still able to open emphasis.</summary>
    internal readonly Span<int> Stack { get; }

    /// <summary>Gets the number of runs recorded.</summary>
    internal int RunCount { get; private set; }

    /// <summary>Gets the number of pairs recorded.</summary>
    internal int PairCount { get; private set; }

    /// <summary>Gets or sets the number of run indexes on the stack.</summary>
    internal int StackCount { get; set; }

    /// <summary>Gets or sets the number of emphasis spans currently open around the render position.</summary>
    internal int Depth { get; set; }

    /// <summary>Gets or sets a value indicating whether inline links render as literal text because the render position has not yet passed an inline element of the enclosing link label.</summary>
    internal bool LinksBlocked { get; set; }

    /// <summary>Gets or sets the index of the first run that ends after the render position.</summary>
    private int Cursor { get; set; }

    /// <summary>Records a run.</summary>
    /// <param name="run">The run.</param>
    /// <returns>Index of the recorded run.</returns>
    internal int AddRun(EmphasisRun run)
    {
        var index = RunCount;
        Runs[index] = run;
        RunCount = index + 1;
        return index;
    }

    /// <summary>Records a pair.</summary>
    /// <param name="pair">The pair.</param>
    /// <returns>One-based index of the recorded pair.</returns>
    internal int AddPair(EmphasisPair pair)
    {
        Pairs[PairCount] = pair;
        PairCount++;
        return PairCount;
    }

    /// <summary>Pushes a run index onto the stack of runs still able to open emphasis.</summary>
    /// <param name="runIndex">Index of the run.</param>
    internal void Push(int runIndex)
    {
        Stack[StackCount] = runIndex;
        StackCount++;
    }

    /// <summary>Finds the recorded run that covers a position, moving the cursor forward past earlier runs.</summary>
    /// <param name="position">Index in the source; never smaller than on the previous call.</param>
    /// <returns>Index of the run, or -1 when no recorded run covers the position.</returns>
    internal int FindRun(int position)
    {
        while (Cursor < RunCount && Runs[Cursor].End <= position)
        {
            Cursor++;
        }

        return Cursor < RunCount && Runs[Cursor].Start <= position ? Cursor : -1;
    }
}
