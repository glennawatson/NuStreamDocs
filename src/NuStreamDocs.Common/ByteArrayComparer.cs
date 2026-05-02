// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;

namespace NuStreamDocs.Common;

/// <summary>
/// Singleton ordinal-byte equality comparer for <see cref="byte"/>
/// arrays — content-equal via vectorized <see cref="System.MemoryExtensions.SequenceEqual{T}(System.ReadOnlySpan{T}, System.ReadOnlySpan{T})"/>,
/// hashed via the runtime's <see cref="HashCode.AddBytes(ReadOnlySpan{byte})"/> (xxHash32 with a per-process random seed).
/// </summary>
/// <remarks>
/// Drop-in replacement for <see cref="System.Collections.Generic.EqualityComparer{T}.Default"/>
/// when keying dictionaries / sets on raw UTF-8 byte arrays — slug
/// dedup tables, anchor-id sets, byte-keyed corpus lookups, and so
/// on. xxHash32 has stronger distribution / avalanche than FNV-1a
/// and processes 4 bytes per iteration with SIMD-friendly mixing,
/// so for inputs above ~8 bytes (typical slug / anchor / URL
/// length) it ties or beats the hand-rolled alternative; for short
/// inputs the SequenceEqual collision-resolution cost dominates
/// either way.
/// </remarks>
public sealed class ByteArrayComparer : IEqualityComparer<byte[]>, IEqualityComparer
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
        var hash = default(HashCode);
        hash.AddBytes(obj);
        return hash.ToHashCode();
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
}
