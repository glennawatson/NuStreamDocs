// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO.Compression;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Builds minimal, structurally valid sfnt and woff2 byte blobs for metric-reader tests.</summary>
internal static class StubFont
{
    /// <summary>Builds an sfnt (ttf-style) blob with <c>head</c>, <c>hhea</c>, and <c>OS/2</c> v2 tables carrying the given metrics.</summary>
    /// <param name="unitsPerEm">Design units per em.</param>
    /// <param name="ascender">Typographic ascender.</param>
    /// <param name="descender">Typographic descender.</param>
    /// <param name="lineGap">Typographic line gap.</param>
    /// <param name="xHeight">x-height (stored as OS/2 v2 <c>sxHeight</c>).</param>
    /// <param name="capHeight">Cap height (stored as OS/2 v2 <c>sCapHeight</c>).</param>
    /// <returns>The sfnt bytes.</returns>
    public static byte[] BuildSfnt(
        ushort unitsPerEm,
        short ascender,
        short descender,
        short lineGap,
        short xHeight,
        short capHeight)
    {
        var headBody = new byte[54];
        BinaryPrimitives.WriteUInt16BigEndian(headBody.AsSpan(18), unitsPerEm);

        var hheaBody = new byte[36];
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(4), ascender);
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(6), descender);
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(8), lineGap);

        var os2Body = new byte[96];
        BinaryPrimitives.WriteUInt16BigEndian(os2Body.AsSpan(0), 2);
        BinaryPrimitives.WriteInt16BigEndian(os2Body.AsSpan(86), xHeight);
        BinaryPrimitives.WriteInt16BigEndian(os2Body.AsSpan(88), capHeight);

        uint[] tags = [0x4F532F32, 0x68656164, 0x68686561];
        byte[][] bodies = [os2Body, headBody, hheaBody];

        var headerSize = 12 + (bodies.Length * 16);
        var total = headerSize;
        for (var i = 0; i < bodies.Length; i++)
        {
            total += bodies[i].Length;
        }

        var sfnt = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(sfnt.AsSpan(0), 0x00010000);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(4), (ushort)bodies.Length);

        var bodyOffset = headerSize;
        for (var i = 0; i < bodies.Length; i++)
        {
            var rec = sfnt.AsSpan(12 + (i * 16));
            BinaryPrimitives.WriteUInt32BigEndian(rec, tags[i]);
            BinaryPrimitives.WriteUInt32BigEndian(rec[8..], (uint)bodyOffset);
            BinaryPrimitives.WriteUInt32BigEndian(rec[12..], (uint)bodies[i].Length);
            bodies[i].CopyTo(sfnt.AsSpan(bodyOffset));
            bodyOffset += bodies[i].Length;
        }

        return sfnt;
    }

    /// <summary>Wraps the same three tables in a minimal woff2 container (Brotli-compressed, untransformed, no inter-table padding).</summary>
    /// <param name="unitsPerEm">Design units per em.</param>
    /// <param name="ascender">Typographic ascender.</param>
    /// <param name="descender">Typographic descender.</param>
    /// <param name="lineGap">Typographic line gap.</param>
    /// <param name="xHeight">x-height.</param>
    /// <param name="capHeight">Cap height.</param>
    /// <returns>The woff2 bytes.</returns>
    public static byte[] BuildWoff2(
        ushort unitsPerEm,
        short ascender,
        short descender,
        short lineGap,
        short xHeight,
        short capHeight)
    {
        var headBody = new byte[54];
        BinaryPrimitives.WriteUInt16BigEndian(headBody.AsSpan(18), unitsPerEm);

        var hheaBody = new byte[36];
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(4), ascender);
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(6), descender);
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(8), lineGap);

        var os2Body = new byte[96];
        BinaryPrimitives.WriteUInt16BigEndian(os2Body.AsSpan(0), 2);
        BinaryPrimitives.WriteInt16BigEndian(os2Body.AsSpan(86), xHeight);
        BinaryPrimitives.WriteInt16BigEndian(os2Body.AsSpan(88), capHeight);

        // Known-tag indices: OS/2=6, head=1, hhea=2. Directory order matches data order; no transform.
        byte[] knownIndices = [6, 1, 2];
        byte[][] bodies = [os2Body, headBody, hheaBody];

        var blockSize = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            blockSize += bodies[i].Length;
        }

        var block = new byte[blockSize];
        var off = 0;
        for (var i = 0; i < bodies.Length; i++)
        {
            bodies[i].CopyTo(block.AsSpan(off));
            off += bodies[i].Length;
        }

        var compressed = new byte[BrotliEncoder.GetMaxCompressedLength(block.Length)];
        if (!BrotliEncoder.TryCompress(block, compressed, out var compressedLen))
        {
            throw new InvalidOperationException("brotli compress failed");
        }

        var dir = new List<byte>();
        for (var i = 0; i < bodies.Length; i++)
        {
            dir.Add(knownIndices[i]);
            WriteUIntBase128(dir, (uint)bodies[i].Length);
        }

        var dirBytes = dir.ToArray();
        const int HeaderSize = 48;
        var totalSfntSize = 12 + (bodies.Length * 16) + blockSize;
        var woff2 = new byte[HeaderSize + dirBytes.Length + compressedLen];
        var h = woff2.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(h[0..], 0x774F4632);
        BinaryPrimitives.WriteUInt32BigEndian(h[4..], 0x00010000);
        BinaryPrimitives.WriteUInt32BigEndian(h[8..], (uint)woff2.Length);
        BinaryPrimitives.WriteUInt16BigEndian(h[12..], (ushort)bodies.Length);
        BinaryPrimitives.WriteUInt16BigEndian(h[14..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(h[16..], (uint)totalSfntSize);
        BinaryPrimitives.WriteUInt32BigEndian(h[20..], (uint)compressedLen);
        dirBytes.CopyTo(h[HeaderSize..]);
        compressed.AsSpan(0, compressedLen).CopyTo(h[(HeaderSize + dirBytes.Length)..]);
        return woff2;
    }

    /// <summary>Appends <paramref name="value"/> in the woff2 UIntBase128 encoding.</summary>
    /// <param name="sink">Destination byte list.</param>
    /// <param name="value">Value to encode.</param>
    private static void WriteUIntBase128(List<byte> sink, uint value)
    {
        Span<byte> groups = stackalloc byte[5];
        var first = 5;
        do
        {
            groups[--first] = (byte)(value & 0x7F);
            value >>= 7;
        }
        while (value != 0);

        for (var i = first; i < 5; i++)
        {
            sink.Add(i == 4 ? groups[i] : (byte)(groups[i] | 0x80));
        }
    }
}
