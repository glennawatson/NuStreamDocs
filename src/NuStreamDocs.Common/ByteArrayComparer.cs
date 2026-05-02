// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

namespace NuStreamDocs.Common;

/// <summary>
/// Singleton ordinal-byte equality comparer for <see cref="byte"/>
/// arrays — content-equal via vectorized <see cref="System.MemoryExtensions.SequenceEqual{T}(System.ReadOnlySpan{T}, System.ReadOnlySpan{T})"/>,
/// hashed via the runtime's span content-hash.
/// </summary>
/// <remarks>
/// Drop-in replacement for <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>
/// when keying dictionaries / sets on raw UTF-8 byte arrays — slug
/// dedup tables, anchor-id sets, byte-keyed corpus lookups, and so
/// on. Implements <see cref="IAlternateEqualityComparer{TAlternate, TKey}"/>
/// so byte-array keyed dictionaries can be probed directly with a
/// <see cref="ReadOnlySpan{T}"/> — no per-lookup byte[] allocation.
/// </remarks>
public sealed class ByteArrayComparer
    : IEqualityComparer<byte[]>,
      IEqualityComparer,
      IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    /// <summary>Initializes a new instance of the <see cref="ByteArrayComparer"/> class.</summary>
    private ByteArrayComparer()
    {
    }

    /// <summary>Gets the shared comparer instance.</summary>
    public static ByteArrayComparer Instance { get; } = new();

    /// <inheritdoc/>
    public bool Equals(byte[]? x, byte[]? y) =>
        x is null
            ? y is null
            : y is not null && x.AsSpan().SequenceEqual(y);

    /// <inheritdoc/>
    public int GetHashCode(byte[] obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return GetSpanHashCode(obj);
    }

    /// <inheritdoc/>
    bool IEqualityComparer.Equals(object? x, object? y) =>
        (x, y) switch
        {
            (null, null) => true,
            (byte[] xb, byte[] yb) => Equals(xb, yb),
            _ => false,
        };

    /// <inheritdoc/>
    int IEqualityComparer.GetHashCode(object obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return obj is byte[] bytes ? GetHashCode(bytes) : obj.GetHashCode();
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Powers <see cref="System.Collections.Generic.Dictionary{TKey, TValue}.GetAlternateLookup{TAlternate}()"/>
    /// so byte-keyed dictionaries can be probed with a
    /// <see cref="ReadOnlySpan{T}"/> directly — no per-lookup
    /// <c>byte[]</c> allocation.
    /// </remarks>
    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) =>
        other is not null && alternate.SequenceEqual(other);

    /// <inheritdoc/>
    public int GetHashCode(ReadOnlySpan<byte> alternate) => GetSpanHashCode(alternate);

    /// <inheritdoc/>
    public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();

    /// <summary>Hashes the bytes of <paramref name="bytes"/> via <see cref="HashCode.AddBytes(ReadOnlySpan{byte})"/>.</summary>
    /// <param name="bytes">Span to hash.</param>
    /// <returns>32-bit hash code suitable for dictionary keying.</returns>
    /// <remarks>
    /// The BCL doesn't expose a content-hash on
    /// <see cref="ReadOnlySpan{T}"/> (the instance method throws), so
    /// <see cref="HashCode"/> is the only supported route. Centralized
    /// so both array and span overloads agree byte-for-byte.
    /// </remarks>
    private static int GetSpanHashCode(ReadOnlySpan<byte> bytes)
    {
        var hash = default(HashCode);
        hash.AddBytes(bytes);
        return hash.ToHashCode();
    }
}
