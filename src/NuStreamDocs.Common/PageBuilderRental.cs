// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.Common;

/// <summary>
/// Disposable rental wrapping a pooled <see cref="ArrayBufferWriter{T}"/>
/// of UTF-8 bytes.
/// </summary>
/// <remarks>
/// <see langword="readonly struct"/> so the rental itself never
/// allocates. Callers <c>using var rental = PageBuilderPool.Rent(...)</c>
/// and write through <see cref="Writer"/>; on dispose the writer is
/// reset and parked back in the pool.
/// </remarks>
public readonly struct PageBuilderRental : IDisposable, IEquatable<PageBuilderRental>
{
    /// <summary>Initializes a new instance of the <see cref="PageBuilderRental"/> struct.</summary>
    /// <param name="writer">The pooled writer this rental owns until dispose.</param>
    internal PageBuilderRental(ArrayBufferWriter<byte> writer) => Writer = writer;

    /// <summary>Gets the rented UTF-8 buffer writer.</summary>
    public ArrayBufferWriter<byte> Writer { get; }

    /// <summary>Equality compares the rented writer reference.</summary>
    /// <param name="left">Left side.</param>
    /// <param name="right">Right side.</param>
    /// <returns>True when both rentals own the same writer.</returns>
    public static bool operator ==(in PageBuilderRental left, in PageBuilderRental right) => left.Equals(right);

    /// <summary>Inequality compares the rented writer reference.</summary>
    /// <param name="left">Left side.</param>
    /// <param name="right">Right side.</param>
    /// <returns>True when the rentals own different writers.</returns>
    public static bool operator !=(in PageBuilderRental left, in PageBuilderRental right) => !left.Equals(right);

    /// <summary>Resets the writer and returns it to the pool.</summary>
    public void Dispose()
    {
        if (Writer is null)
        {
            return;
        }

        Writer.ResetWrittenCount();
        PageBuilderPool.Return(Writer);
    }

    /// <inheritdoc/>
    public bool Equals(PageBuilderRental other) => ReferenceEquals(Writer, other.Writer);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PageBuilderRental other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Writer is null ? 0 : RuntimeHelpers.GetHashCode(Writer);
}
