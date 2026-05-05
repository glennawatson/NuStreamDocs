// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

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

    /// <summary>Emits <c>{prefix}{value}"</c> when <paramref name="value"/> is non-empty.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="prefix">Open-attribute prefix including <c>="</c>.</param>
    /// <param name="value">UTF-8 attribute value bytes; skipped when empty.</param>
    public static void AppendAttribute(IBufferWriter<byte> writer, ReadOnlySpan<byte> prefix, ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        WriteUtf8(writer, prefix);
        WriteUtf8(writer, value);
        WriteUtf8(writer, "\""u8);
    }
}
