// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace NuStreamDocs.Html;

/// <summary>Scope of one rented <see cref="NestedContentScratch"/>; disposing releases it.</summary>
internal readonly struct NestedContentRental : IDisposable, IEquatable<NestedContentRental>
{
    /// <summary>Nesting depth of the rental.</summary>
    private readonly int _index;

    /// <summary>Initializes a new instance of the <see cref="NestedContentRental"/> struct.</summary>
    /// <param name="scratch">Rented buffers.</param>
    /// <param name="index">Nesting depth of the rental.</param>
    internal NestedContentRental(NestedContentScratch scratch, int index)
    {
        Scratch = scratch;
        _index = index;
    }

    /// <summary>Gets the rented buffers.</summary>
    internal NestedContentScratch Scratch { get; }

    /// <summary>Equality compares the rented buffers and depth.</summary>
    /// <param name="left">Left side.</param>
    /// <param name="right">Right side.</param>
    /// <returns>True when both rentals own the same buffers at the same depth.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(in NestedContentRental left, in NestedContentRental right) => left.Equals(right);

    /// <summary>Inequality compares the rented buffers and depth.</summary>
    /// <param name="left">Left side.</param>
    /// <param name="right">Right side.</param>
    /// <returns>True when the rentals differ.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(in NestedContentRental left, in NestedContentRental right) => !left.Equals(right);

    /// <summary>Releases the buffers.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose()
    {
        if (Scratch is null)
        {
            return;
        }

        NestedContentScratch.Return(Scratch, _index);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(NestedContentRental other) => ReferenceEquals(Scratch, other.Scratch) && _index == other._index;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is NestedContentRental other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Scratch is null ? 0 : RuntimeHelpers.GetHashCode(Scratch) ^ _index;
}
