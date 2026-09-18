// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO.Compression;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Builds minimal, structurally valid sfnt and woff2 byte blobs for metric-reader tests.</summary>
internal static class StubFont
{
    /// <summary>Length of the head table.</summary>
    private const int HeadTableLength = 54;

    /// <summary>Offset of units per em in the head table.</summary>
    private const int HeadUnitsPerEmOffset = 18;

    /// <summary>Length of the horizontal header table.</summary>
    private const int HheaTableLength = 36;

    /// <summary>Offset of the ascender in the horizontal header.</summary>
    private const int HheaAscenderOffset = 4;

    /// <summary>Offset of the descender in the horizontal header.</summary>
    private const int HheaDescenderOffset = 6;

    /// <summary>Offset of the line gap in the horizontal header.</summary>
    private const int HheaLineGapOffset = 8;

    /// <summary>Length of the OS/2 table.</summary>
    private const int Os2TableLength = 96;

    /// <summary>OS/2 version supporting lowercase and capital letter heights.</summary>
    private const int Os2Version = 2;

    /// <summary>Offset of the lowercase letter height in the OS/2 table.</summary>
    private const int Os2XHeightOffset = 86;

    /// <summary>Offset of the capital letter height in the OS/2 table.</summary>
    private const int Os2CapHeightOffset = 88;

    /// <summary>Tag identifying the OS/2 table.</summary>
    private const int Os2Tag = 0x4F532F32;

    /// <summary>Tag identifying the head table.</summary>
    private const int HeadTag = 0x68656164;

    /// <summary>Tag identifying the horizontal header table.</summary>
    private const int HheaTag = 0x68686561;

    /// <summary>Length of the sfnt header.</summary>
    private const int SfntHeaderLength = 12;

    /// <summary>Length of each sfnt table directory record.</summary>
    private const int SfntRecordLength = 16;

    /// <summary>Version identifying TrueType outlines.</summary>
    private const int SfntVersion = 0x00010000;

    /// <summary>Offset of the table count in the sfnt header.</summary>
    private const int SfntTableCountOffset = 4;

    /// <summary>Offset of the table location within a directory record.</summary>
    private const int SfntRecordOffsetOffset = 8;

    /// <summary>Offset of the table length within a directory record.</summary>
    private const int SfntRecordLengthOffset = 12;

    /// <summary>WOFF2 index identifying the OS/2 table.</summary>
    private const int Os2KnownTagIndex = 6;

    /// <summary>WOFF2 index identifying the horizontal header.</summary>
    private const int HheaKnownTagIndex = 2;

    /// <summary>Signature identifying a WOFF2 file.</summary>
    private const int Woff2Signature = 0x774F4632;

    /// <summary>Offset of the sfnt flavor in the WOFF2 header.</summary>
    private const int Woff2FlavorOffset = 4;

    /// <summary>Offset of the file length in the WOFF2 header.</summary>
    private const int Woff2LengthOffset = 8;

    /// <summary>Offset of the table count in the WOFF2 header.</summary>
    private const int Woff2TableCountOffset = 12;

    /// <summary>Offset of the reserved field in the WOFF2 header.</summary>
    private const int Woff2ReservedOffset = 14;

    /// <summary>Offset of the reconstructed sfnt length in the WOFF2 header.</summary>
    private const int Woff2SfntLengthOffset = 16;

    /// <summary>Offset of the compressed data length in the WOFF2 header.</summary>
    private const int Woff2CompressedLengthOffset = 20;

    /// <summary>Maximum number of groups for a base-128 unsigned integer.</summary>
    private const int MaxBase128Groups = 5;

    /// <summary>Mask selecting a base-128 group's data bits.</summary>
    private const int Base128DataMask = 0x7F;

    /// <summary>Number of data bits in a base-128 group.</summary>
    private const int Base128DataBits = 7;

    /// <summary>Index of the least significant base-128 group.</summary>
    private const int Base128LastGroupIndex = 4;

    /// <summary>Bit indicating that another base-128 group follows.</summary>
    private const int Base128ContinuationBit = 0x80;

    /// <summary>Builds an sfnt (ttf-style) blob with <c>head</c>, <c>hhea</c>, and <c>OS/2</c> v2 tables carrying the given metrics.</summary>
    /// <param name="unitsPerEm">Design units per em.</param>
    /// <param name="ascender">Typographic ascender.</param>
    /// <param name="descender">Typographic descender.</param>
    /// <param name="lineGap">Typographic line gap.</param>
    /// <param name="lowercaseHeight">Height of a lowercase x.</param>
    /// <param name="capHeight">Cap height (stored as OS/2 v2 <c>sCapHeight</c>).</param>
    /// <returns>The sfnt bytes.</returns>
    internal static byte[] BuildSfnt(
        ushort unitsPerEm,
        short ascender,
        short descender,
        short lineGap,
        short lowercaseHeight,
        short capHeight)
    {
        var headBody = new byte[HeadTableLength];
        BinaryPrimitives.WriteUInt16BigEndian(headBody.AsSpan(HeadUnitsPerEmOffset), unitsPerEm);

        var hheaBody = new byte[HheaTableLength];
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(HheaAscenderOffset), ascender);
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(HheaDescenderOffset), descender);
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(HheaLineGapOffset), lineGap);

        var os2Body = new byte[Os2TableLength];
        BinaryPrimitives.WriteUInt16BigEndian(os2Body.AsSpan(0), Os2Version);
        BinaryPrimitives.WriteInt16BigEndian(os2Body.AsSpan(Os2XHeightOffset), lowercaseHeight);
        BinaryPrimitives.WriteInt16BigEndian(os2Body.AsSpan(Os2CapHeightOffset), capHeight);

        uint[] tags = [Os2Tag, HeadTag, HheaTag];
        byte[][] bodies = [os2Body, headBody, hheaBody];

        var headerSize = SfntHeaderLength + (bodies.Length * SfntRecordLength);
        var total = headerSize;
        for (var i = 0; i < bodies.Length; i++)
        {
            total += bodies[i].Length;
        }

        var sfnt = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(sfnt.AsSpan(0), SfntVersion);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(SfntTableCountOffset), (ushort)bodies.Length);

        var bodyOffset = headerSize;
        for (var i = 0; i < bodies.Length; i++)
        {
            var rec = sfnt.AsSpan(SfntHeaderLength + (i * SfntRecordLength));
            BinaryPrimitives.WriteUInt32BigEndian(rec, tags[i]);
            BinaryPrimitives.WriteUInt32BigEndian(rec[SfntRecordOffsetOffset..], (uint)bodyOffset);
            BinaryPrimitives.WriteUInt32BigEndian(rec[SfntRecordLengthOffset..], (uint)bodies[i].Length);
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
    /// <param name="lowercaseHeight">Height of a lowercase x.</param>
    /// <param name="capHeight">Cap height.</param>
    /// <returns>The woff2 bytes.</returns>
    /// <exception cref="InvalidOperationException">The font tables cannot be compressed.</exception>
    internal static byte[] BuildWoff2(
        ushort unitsPerEm,
        short ascender,
        short descender,
        short lineGap,
        short lowercaseHeight,
        short capHeight)
    {
        var headBody = new byte[HeadTableLength];
        BinaryPrimitives.WriteUInt16BigEndian(headBody.AsSpan(HeadUnitsPerEmOffset), unitsPerEm);

        var hheaBody = new byte[HheaTableLength];
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(HheaAscenderOffset), ascender);
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(HheaDescenderOffset), descender);
        BinaryPrimitives.WriteInt16BigEndian(hheaBody.AsSpan(HheaLineGapOffset), lineGap);

        var os2Body = new byte[Os2TableLength];
        BinaryPrimitives.WriteUInt16BigEndian(os2Body.AsSpan(0), Os2Version);
        BinaryPrimitives.WriteInt16BigEndian(os2Body.AsSpan(Os2XHeightOffset), lowercaseHeight);
        BinaryPrimitives.WriteInt16BigEndian(os2Body.AsSpan(Os2CapHeightOffset), capHeight);

        // Known-tag indices: OS/2=6, head=1, hhea=2. Directory order matches data order; no transform.
        byte[] knownIndices = [Os2KnownTagIndex, 1, HheaKnownTagIndex];
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
        var totalSfntSize = SfntHeaderLength + (bodies.Length * SfntRecordLength) + blockSize;
        var woff2 = new byte[HeaderSize + dirBytes.Length + compressedLen];
        var h = woff2.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(h[0..], Woff2Signature);
        BinaryPrimitives.WriteUInt32BigEndian(h[Woff2FlavorOffset..], SfntVersion);
        BinaryPrimitives.WriteUInt32BigEndian(h[Woff2LengthOffset..], (uint)woff2.Length);
        BinaryPrimitives.WriteUInt16BigEndian(h[Woff2TableCountOffset..], (ushort)bodies.Length);
        BinaryPrimitives.WriteUInt16BigEndian(h[Woff2ReservedOffset..], 0);
        BinaryPrimitives.WriteUInt32BigEndian(h[Woff2SfntLengthOffset..], (uint)totalSfntSize);
        BinaryPrimitives.WriteUInt32BigEndian(h[Woff2CompressedLengthOffset..], (uint)compressedLen);
        dirBytes.CopyTo(h[HeaderSize..]);
        compressed.AsSpan(0, compressedLen).CopyTo(h[(HeaderSize + dirBytes.Length)..]);
        return woff2;
    }

    /// <summary>Appends <paramref name="value"/> in the woff2 UIntBase128 encoding.</summary>
    /// <param name="sink">Destination byte list.</param>
    /// <param name="value">Value to encode.</param>
    private static void WriteUIntBase128(List<byte> sink, uint value)
    {
        Span<byte> groups = stackalloc byte[MaxBase128Groups];
        var first = MaxBase128Groups;
        do
        {
            first--;
            groups[first] = (byte)(value & Base128DataMask);
            value >>= Base128DataBits;
        }
        while (value != 0);

        for (var i = first; i < MaxBase128Groups; i++)
        {
            sink.Add(i == Base128LastGroupIndex ? groups[i] : (byte)(groups[i] | Base128ContinuationBit));
        }
    }
}
