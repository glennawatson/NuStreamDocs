// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace NuStreamDocs.Common;

/// <summary>
/// Direct UTF-16 → UTF-8 helpers that both <c>TagsIndexWriter</c> and
/// <c>BlogIndexEmitter</c> reach for when emitting HTML/JSON. Centralized
/// here so the duplication-detector stops flagging the per-emitter copies.
/// </summary>
public static class Utf8StringWriter
{
    /// <summary>UTF-8 encodes <paramref name="value"/> directly into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="value">Source text; null/empty is a no-op.</param>
    public static void Write(IBufferWriter<byte> writer, string value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var max = Encoding.UTF8.GetMaxByteCount(value.Length);
        var dst = writer.GetSpan(max);
        var written = Encoding.UTF8.GetBytes(value, dst);
        writer.Advance(written);
    }

    /// <summary>Bulk-copies <paramref name="bytes"/> into <paramref name="writer"/>, skipping the empty case so backing implementations are not asked for a zero-length span.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">Bytes to copy; an empty span is a no-op.</param>
    public static void Write(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (bytes.IsEmpty)
        {
            return;
        }

        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>Writes a single byte into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="value">Byte to write.</param>
    public static void WriteByte(IBufferWriter<byte> writer, byte value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var dst = writer.GetSpan(1);
        dst[0] = value;
        writer.Advance(1);
    }

    /// <summary>Writes <paramref name="value"/> as ASCII digits into <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="value">Integer to format.</param>
    public static void WriteInt32(IBufferWriter<byte> writer, int value)
    {
        ArgumentNullException.ThrowIfNull(writer);

        Span<byte> buffer = stackalloc byte[16];
        if (!value.TryFormat(buffer, out var written, default, CultureInfo.InvariantCulture))
        {
            return;
        }

        var dst = writer.GetSpan(written);
        buffer[..written].CopyTo(dst);
        writer.Advance(written);
    }
}
