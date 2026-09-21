// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Markdown;

/// <summary>A run of identical emphasis marker bytes that can open or close emphasis.</summary>
/// <param name="Start">Index of the first marker byte.</param>
/// <param name="Length">Number of marker bytes.</param>
/// <param name="Underscore">True for underscore markers, false for asterisks.</param>
/// <param name="CanOpen">True when the run can open emphasis.</param>
/// <param name="CanClose">True when the run can close emphasis.</param>
internal record struct EmphasisRun(int Start, int Length, bool Underscore, bool CanOpen, bool CanClose)
{
    /// <summary>Gets or sets the number of leading marker bytes paired as a closer.</summary>
    internal int CloserUsed { get; set; }

    /// <summary>Gets or sets the number of trailing marker bytes paired as an opener.</summary>
    internal int OpenerUsed { get; set; }

    /// <summary>Gets or sets the one-based index of the outermost pair opened by this run, or zero when it opens none.</summary>
    internal int FirstPair { get; set; }

    /// <summary>Gets the index one past the last marker byte.</summary>
    internal readonly int End => Start + Length;

    /// <summary>Gets the number of marker bytes not yet paired.</summary>
    internal readonly int Remaining => Length - CloserUsed - OpenerUsed;
}
