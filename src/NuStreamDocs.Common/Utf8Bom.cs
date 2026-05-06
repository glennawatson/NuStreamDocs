// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Common;

/// <summary>UTF-8 byte-order-mark (<c>EF BB BF</c>) helpers for byte-level scanners.</summary>
public static class Utf8Bom
{
    /// <summary>First byte of the UTF-8 BOM.</summary>
    private const byte Byte0 = 0xEF;

    /// <summary>Second byte of the UTF-8 BOM.</summary>
    private const byte Byte1 = 0xBB;

    /// <summary>Third byte of the UTF-8 BOM.</summary>
    private const byte Byte2 = 0xBF;

    /// <summary>Encoded length of the UTF-8 BOM.</summary>
    private const int EncodedLength = 3;

    /// <summary>Gets the length of the UTF-8 byte-order mark.</summary>
    public static int Length => EncodedLength;

    /// <summary>Returns the BOM length when <paramref name="source"/> begins with <c>EF BB BF</c>; zero otherwise.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <returns><see cref="Length"/> when present, <c>0</c> otherwise.</returns>
    public static int LengthOf(ReadOnlySpan<byte> source) =>
        source is [Byte0, Byte1, Byte2, ..] ? Length : 0;

    /// <summary>Returns <paramref name="source"/> with any leading UTF-8 BOM removed.</summary>
    /// <param name="source">UTF-8 source bytes.</param>
    /// <returns>The source advanced past a leading BOM, or the input unchanged.</returns>
    public static ReadOnlySpan<byte> Strip(ReadOnlySpan<byte> source) =>
        source[LengthOf(source)..];

    /// <summary>Returns <paramref name="bytes"/> with any leading UTF-8 BOM removed; returns the same array reference when no BOM is present.</summary>
    /// <param name="bytes">UTF-8 bytes.</param>
    /// <returns>The original array, or a fresh BOM-trimmed copy.</returns>
    public static byte[] StripIfPresent(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var bomLength = LengthOf(bytes);
        return bomLength is 0 ? bytes : bytes.AsSpan(bomLength).ToArray();
    }
}
