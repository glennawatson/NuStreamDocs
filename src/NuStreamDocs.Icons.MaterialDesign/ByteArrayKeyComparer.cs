// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>
/// <see cref="IEqualityComparer{T}"/> for <c>byte[]</c> keys that also
/// implements <see cref="IAlternateEqualityComparer{TAlternate, T}"/>
/// for <see cref="ReadOnlySpan{T}"/> alternates so a
/// <see cref="System.Collections.Frozen.FrozenDictionary{TKey, TValue}"/>
/// keyed on <c>byte[]</c> can serve <see cref="ReadOnlySpan{T}"/> lookups
/// with no per-call <c>byte[]</c> allocation.
/// </summary>
internal sealed class ByteArrayKeyComparer : IEqualityComparer<byte[]>, IAlternateEqualityComparer<ReadOnlySpan<byte>, byte[]>
{
    /// <summary>Gets the singleton instance.</summary>
    public static ByteArrayKeyComparer Instance { get; } = new();

    /// <inheritdoc/>
    public bool Equals(byte[]? x, byte[]? y) => x is null ? y is null : y is not null && x.AsSpan().SequenceEqual(y);

    /// <inheritdoc/>
    public int GetHashCode(byte[] obj)
    {
        ArgumentNullException.ThrowIfNull(obj);
        return GetHashCode((ReadOnlySpan<byte>)obj);
    }

    /// <inheritdoc/>
    public bool Equals(ReadOnlySpan<byte> alternate, byte[] other) => other is not null && alternate.SequenceEqual(other);

    /// <inheritdoc/>
    public int GetHashCode(ReadOnlySpan<byte> alternate)
    {
        // FNV-1a 32-bit — small, fast, and stable across runs (we don't need DoS resistance for a built-once frozen table).
        const uint FnvOffsetBasis = 2_166_136_261u;
        const uint FnvPrime = 16_777_619u;
        var hash = FnvOffsetBasis;
        for (var i = 0; i < alternate.Length; i++)
        {
            hash = (hash ^ alternate[i]) * FnvPrime;
        }

        return (int)hash;
    }

    /// <inheritdoc/>
    public byte[] Create(ReadOnlySpan<byte> alternate) => alternate.ToArray();
}
