// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Html;

/// <summary>Reusable working buffers for rendering one level of nested content (a list item or block quote body).</summary>
internal sealed class NestedContentScratch
{
    /// <summary>Initial capacity of the body buffer.</summary>
    private const int InitialBodyCapacity = 256;

    /// <summary>Number of nesting levels whose buffers are kept per thread; deeper levels use unpooled buffers.</summary>
    private const int MaxCachedDepth = 32;

    /// <summary>Body capacity above which a buffer is dropped on return instead of kept.</summary>
    private const int MaxCachedBodyCapacity = 256 * 1024;

    /// <summary>Per-thread buffers indexed by nesting depth.</summary>
    [ThreadStatic]
    private static NestedContentScratch?[]? _levels;

    /// <summary>Number of rentals currently outstanding on this thread; also the index of the next rental.</summary>
    [ThreadStatic]
    private static int _depth;

    /// <summary>Gets the de-indented body of the nested content.</summary>
    internal ArrayBufferWriter<byte> Body { get; } = new(InitialBodyCapacity);

    /// <summary>Rents the buffers for the next nesting level of the current thread.</summary>
    /// <returns>A rental the caller disposes exactly once.</returns>
    internal static NestedContentRental Rent()
    {
        var index = _depth;
        _depth = index + 1;
        if (index >= MaxCachedDepth)
        {
            return new(new(), index);
        }

        var levels = _levels ??= new NestedContentScratch?[MaxCachedDepth];
        var scratch = levels[index];
        if (scratch is null)
        {
            scratch = new();
            levels[index] = scratch;
            return new(scratch, index);
        }

        scratch.Body.ResetWrittenCount();
        return new(scratch, index);
    }

    /// <summary>Releases the level at <paramref name="index"/> and every level above it.</summary>
    /// <param name="scratch">Buffers of the level being released.</param>
    /// <param name="index">Nesting depth of the level.</param>
    internal static void Return(NestedContentScratch scratch, int index)
    {
        _depth = index;
        if (index >= MaxCachedDepth || scratch.Body.Capacity <= MaxCachedBodyCapacity)
        {
            return;
        }

        _levels![index] = null;
    }
}
