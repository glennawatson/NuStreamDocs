// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Common;

/// <summary>UTF-8 encoding helpers that produce <see cref="byte"/> arrays in one shot — the inverse direction of <see cref="Utf8Snapshot"/>.</summary>
/// <remarks>
/// Use these at the natural string → bytes boundary (config readers, CLI flag values, frontmatter-derived
/// scalars) so per-page hot paths stay byte-only. Each helper folds a common shape (encode + suffix
/// guard, encode + slash normalization, etc.) so call sites don't repeat the byte-count + branch +
/// fixup pattern.
/// </remarks>
public static class Utf8Encoder
{
    /// <summary>Highest single-byte UTF-8 value (anything above this is the leading byte of a multi-byte sequence).</summary>
    private const byte AsciiMax = 0x7F;

    /// <summary>Encodes <paramref name="value"/> to UTF-8 and ensures the result ends with <paramref name="trailingAscii"/>, allocating exactly one fresh byte array.</summary>
    /// <param name="value">Source string; null/empty produces a single-byte array containing <paramref name="trailingAscii"/>.</param>
    /// <param name="trailingAscii">ASCII byte (&lt; 0x80) the result must end with.</param>
    /// <returns>UTF-8 bytes, always ending in <paramref name="trailingAscii"/>.</returns>
    /// <remarks>
    /// Inspects the source <see cref="string"/>'s last <see cref="char"/> directly to decide whether
    /// the trailing byte is needed; valid only for ASCII trailing bytes since multi-byte UTF-8
    /// trailing characters can't be detected from the char index alone. The single allocation is the
    /// returned array itself — the encode writes straight into it.
    /// </remarks>
    public static byte[] EncodeWithTrailingAscii(string? value, byte trailingAscii)
    {
        if (trailingAscii > AsciiMax)
        {
            throw new ArgumentOutOfRangeException(nameof(trailingAscii), "Only ASCII trailing bytes are supported.");
        }

        if (string.IsNullOrEmpty(value))
        {
            return [trailingAscii];
        }

        var hasTrailing = value[^1] == (char)trailingAscii;
        var byteCount = Encoding.UTF8.GetByteCount(value);
        var dst = new byte[byteCount + (hasTrailing ? 0 : 1)];
        Encoding.UTF8.GetBytes(value, dst);
        if (!hasTrailing)
        {
            dst[byteCount] = trailingAscii;
        }

        return dst;
    }

    /// <summary>Encodes <paramref name="value"/> to a fresh UTF-8 byte array; returns an empty array for null / empty input.</summary>
    /// <param name="value">Source string.</param>
    /// <returns>UTF-8 bytes.</returns>
    public static byte[] Encode(string? value) =>
        string.IsNullOrEmpty(value) ? [] : Encoding.UTF8.GetBytes(value);

    /// <summary>Encodes every entry of <paramref name="values"/> into UTF-8 byte arrays.</summary>
    /// <param name="values">Source strings; null / empty input yields an empty result.</param>
    /// <returns>Per-entry UTF-8 bytes; <c>null</c> / empty entries map to empty arrays so byte-shaped consumers can skip them with a length check.</returns>
    public static byte[][] EncodeArray(string[]? values)
    {
        if (values is null or [])
        {
            return [];
        }

        var result = new byte[values.Length][];
        for (var i = 0; i < values.Length; i++)
        {
            result[i] = Encode(values[i]);
        }

        return result;
    }
}
