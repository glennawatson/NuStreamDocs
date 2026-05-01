// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Provides utility methods for writing UTF-8 encoded content to a specified buffer writer.
/// </summary>
public static class HeadExtraWriter
{
    /// <summary>Bulk-writes <paramref name="bytes"/> to <paramref name="writer"/>.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">UTF-8 literal.</param>
    public static void WriteUtf8(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var dst = writer.GetSpan(bytes.Length);
        bytes.CopyTo(dst);
        writer.Advance(bytes.Length);
    }

    /// <summary>UTF-8-encodes <paramref name="value"/> directly into the writer's span.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="value">String to encode.</param>
    public static void WriteString(IBufferWriter<byte> writer, string value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var dst = writer.GetSpan(Encoding.UTF8.GetMaxByteCount(value.Length));
        var written = Encoding.UTF8.GetBytes(value, dst);
        writer.Advance(written);
    }

    /// <summary>Emits <c>{prefix}{value}"</c> when <paramref name="value"/> is non-empty.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="prefix">Open-attribute prefix including <c>="</c>.</param>
    /// <param name="value">Attribute value; skipped when empty.</param>
    public static void AppendAttribute(IBufferWriter<byte> writer, ReadOnlySpan<byte> prefix, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        WriteUtf8(writer, prefix);
        WriteString(writer, value);
        WriteUtf8(writer, "\""u8);
    }
}
