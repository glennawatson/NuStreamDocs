// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Markdown.Common;

/// <summary>
/// Lightweight UTF-8 entity decoder. Handles the five entities the
/// markdown HTML escaper produces: <c>&amp;lt;</c>, <c>&amp;gt;</c>,
/// <c>&amp;amp;</c>, <c>&amp;quot;</c>, and <c>&amp;#39;</c>. Anything
/// else is copied through verbatim.
/// </summary>
public static class HtmlEntityDecoder
{
    /// <summary>Decodes <paramref name="bytes"/> into <paramref name="writer"/>; copies through verbatim when no <c>&amp;</c> is present.</summary>
    /// <param name="writer">UTF-8 sink.</param>
    /// <param name="bytes">UTF-8 input bytes.</param>
    public static void DecodeInto(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var ampersand = bytes.IndexOf((byte)'&');
        switch (ampersand)
        {
            case < 0:
                {
                    if (bytes.Length is 0)
                    {
                        return;
                    }

                    var dst = writer.GetSpan(bytes.Length);
                    bytes.CopyTo(dst);
                    writer.Advance(bytes.Length);
                    return;
                }

            // Copy the leading no-entity prefix in one bulk write before the per-byte scan.
            case > 0:
                {
                    var dst = writer.GetSpan(ampersand);
                    bytes[..ampersand].CopyTo(dst);
                    writer.Advance(ampersand);
                    break;
                }
        }

        var i = ampersand;
        while (i < bytes.Length)
        {
            if (bytes[i] is not (byte)'&')
            {
                AppendByte(writer, bytes[i]);
                i++;
                continue;
            }

            var (replacement, length) = MatchEntity(bytes[i..]);
            if (length is 0)
            {
                AppendByte(writer, bytes[i]);
                i++;
                continue;
            }

            AppendByte(writer, replacement);
            i += length;
        }
    }

    /// <summary>Decodes <paramref name="bytes"/> and returns a fresh array containing the decoded contents.</summary>
    /// <param name="bytes">UTF-8 input bytes.</param>
    /// <returns>Decoded UTF-8 bytes.</returns>
    public static byte[] Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IndexOf((byte)'&') < 0)
        {
            return [.. bytes];
        }

        using var rental = PageBuilderPool.Rent(bytes.Length);
        var sink = rental.Writer;
        DecodeInto(sink, bytes);
        return [.. sink.WrittenSpan];
    }

    /// <summary>Matches the leading entity in <paramref name="slice"/> against the recognized set.</summary>
    /// <param name="slice">Bytes starting at the leading <c>&amp;</c>.</param>
    /// <returns>A tuple of <c>(replacementByte, entityLength)</c>; <c>length</c> is 0 when no entity matched.</returns>
    private static (byte Replacement, int Length) MatchEntity(ReadOnlySpan<byte> slice)
    {
        if (slice.StartsWith("&lt;"u8))
        {
            return ((byte)'<', "&lt;"u8.Length);
        }

        if (slice.StartsWith("&gt;"u8))
        {
            return ((byte)'>', "&gt;"u8.Length);
        }

        if (slice.StartsWith("&amp;"u8))
        {
            return ((byte)'&', "&amp;"u8.Length);
        }

        if (slice.StartsWith("&quot;"u8))
        {
            return ((byte)'"', "&quot;"u8.Length);
        }

        if (slice.StartsWith("&#39;"u8))
        {
            return ((byte)'\'', "&#39;"u8.Length);
        }

        return (0, 0);
    }

    /// <summary>Writes a single byte into <paramref name="writer"/>.</summary>
    /// <param name="writer">Output sink.</param>
    /// <param name="b">Byte to write.</param>
    private static void AppendByte(IBufferWriter<byte> writer, byte b)
    {
        var dst = writer.GetSpan(1);
        dst[0] = b;
        writer.Advance(1);
    }
}
