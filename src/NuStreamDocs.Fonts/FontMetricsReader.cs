// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace NuStreamDocs.Fonts;

/// <summary>Reads font metrics from raw font bytes, sniffing woff2 vs. sfnt (ttf/otf).</summary>
public static class FontMetricsReader
{
    /// <summary>woff2 signature <c>wOF2</c>.</summary>
    private const uint Woff2Signature = 0x774F4632;

    /// <summary>Parses the metrics from <paramref name="fontBytes"/>, whatever container they're in.</summary>
    /// <param name="fontBytes">Raw font file bytes (woff2, ttf, or otf).</param>
    /// <returns>The metrics, or <see langword="null"/> when the data can't be parsed.</returns>
    public static FontMetrics? Read(ReadOnlySpan<byte> fontBytes)
    {
        if (fontBytes.Length >= sizeof(uint) && BinaryPrimitives.ReadUInt32BigEndian(fontBytes) == Woff2Signature)
        {
            return Woff2Reader.TryRead(fontBytes);
        }

        return SfntTableReader.TryRead(fontBytes);
    }
}
