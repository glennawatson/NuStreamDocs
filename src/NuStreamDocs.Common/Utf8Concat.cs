// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>
/// Allocation-aware concatenation helpers for joining UTF-8 byte spans into a single
/// right-sized <c>byte[]</c>. The byte-shaped equivalent of <c>string.Concat</c> — use it
/// anywhere a per-piece <c>StringBuilder</c> or <c>+</c>-chain would otherwise materialize.
/// </summary>
/// <remarks>
/// One pre-sized allocation per call; spans are copied straight into the destination, so
/// callers can mix <c>"..."u8</c> literals, captured <c>byte[]</c> fields, and pooled
/// rentals without forcing intermediate copies. For more than four pieces, switch to
/// <see cref="ConcatMany"/> or the <see cref="System.Buffers.IBufferWriter{T}"/> path
/// directly — the four-arg overload is the largest fixed-arity slot we ship.
/// </remarks>
public static class Utf8Concat
{
    /// <summary>Concatenates two UTF-8 spans into a fresh <c>byte[]</c>.</summary>
    /// <param name="a">First chunk.</param>
    /// <param name="b">Second chunk.</param>
    /// <returns>The combined bytes; an empty array when both inputs are empty.</returns>
    public static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var totalLength = a.Length + b.Length;
        if (totalLength is 0)
        {
            return [];
        }

        var dst = new byte[totalLength];
        a.CopyTo(dst);
        b.CopyTo(dst.AsSpan(a.Length));
        return dst;
    }

    /// <summary>Concatenates three UTF-8 spans into a fresh <c>byte[]</c>.</summary>
    /// <param name="a">First chunk.</param>
    /// <param name="b">Second chunk.</param>
    /// <param name="c">Third chunk.</param>
    /// <returns>The combined bytes.</returns>
    public static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c)
    {
        var totalLength = a.Length + b.Length + c.Length;
        if (totalLength is 0)
        {
            return [];
        }

        var dst = new byte[totalLength];
        a.CopyTo(dst);
        b.CopyTo(dst.AsSpan(a.Length));
        c.CopyTo(dst.AsSpan(a.Length + b.Length));
        return dst;
    }

    /// <summary>Concatenates four UTF-8 spans into a fresh <c>byte[]</c>.</summary>
    /// <param name="a">First chunk.</param>
    /// <param name="b">Second chunk.</param>
    /// <param name="c">Third chunk.</param>
    /// <param name="d">Fourth chunk.</param>
    /// <returns>The combined bytes.</returns>
    public static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, ReadOnlySpan<byte> c, ReadOnlySpan<byte> d)
    {
        var totalLength = a.Length + b.Length + c.Length + d.Length;
        if (totalLength is 0)
        {
            return [];
        }

        var dst = new byte[totalLength];
        a.CopyTo(dst);
        b.CopyTo(dst.AsSpan(a.Length));
        c.CopyTo(dst.AsSpan(a.Length + b.Length));
        d.CopyTo(dst.AsSpan(a.Length + b.Length + c.Length));
        return dst;
    }

    /// <summary>Concatenates an arbitrary number of UTF-8 chunks into a fresh <c>byte[]</c>.</summary>
    /// <param name="parts">Chunks in emission order.</param>
    /// <returns>The combined bytes; an empty array when every chunk is empty.</returns>
    public static byte[] ConcatMany(params ReadOnlySpan<byte[]> parts)
    {
        var total = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(parts[i]);
            total += parts[i].Length;
        }

        if (total is 0)
        {
            return [];
        }

        var dst = new byte[total];
        var write = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            parts[i].AsSpan().CopyTo(dst.AsSpan(write));
            write += parts[i].Length;
        }

        return dst;
    }
}
