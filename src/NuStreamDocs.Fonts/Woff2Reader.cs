// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;

namespace NuStreamDocs.Fonts;

/// <summary>Reads vertical metrics out of a woff2 font by decompressing its table block and reusing <see cref="SfntTableReader"/>.</summary>
public static class Woff2Reader
{
    /// <summary>woff2 signature <c>wOF2</c>.</summary>
    private const uint Woff2Signature = 0x774F4632;

    /// <summary>sfnt version stamped on the synthetic font we hand to <see cref="SfntTableReader"/>.</summary>
    private const uint SfntVersionTrueType = 0x00010000;

    /// <summary>Big-endian tag <c>head</c>.</summary>
    private const uint HeadTag = 0x68656164;

    /// <summary>Big-endian tag <c>hhea</c>.</summary>
    private const uint HheaTag = 0x68686561;

    /// <summary>Big-endian tag <c>OS/2</c>.</summary>
    private const uint Os2Tag = 0x4F532F32;

    /// <summary>Size in bytes of the woff2 header.</summary>
    private const int HeaderSize = 48;

    /// <summary>Offset of <c>numTables</c> in the woff2 header.</summary>
    private const int NumTablesOffset = 12;

    /// <summary>Offset of <c>totalCompressedSize</c> in the woff2 header.</summary>
    private const int TotalCompressedSizeOffset = 20;

    /// <summary>Size in bytes of the sfnt offset table.</summary>
    private const int SfntOffsetTableSize = 12;

    /// <summary>Offset of <c>numTables</c> in the sfnt offset table.</summary>
    private const int SfntNumTablesOffset = 4;

    /// <summary>Size in bytes of one sfnt table record.</summary>
    private const int SfntRecordSize = 16;

    /// <summary>Byte length of the 4-byte arbitrary-tag field.</summary>
    private const int ArbitraryTagSize = 4;

    /// <summary>Offset of the table <c>offset</c> field within an sfnt table record.</summary>
    private const int RecordOffsetField = 8;

    /// <summary>Offset of the table <c>length</c> field within an sfnt table record.</summary>
    private const int RecordLengthField = 12;

    /// <summary>Sentinel known-tag index meaning a 4-byte tag follows the flags byte.</summary>
    private const int ArbitraryTagIndex = 63;

    /// <summary>Mask for the known-tag-index bits of the flags byte.</summary>
    private const int KnownIndexMask = 0x3F;

    /// <summary>Right-shift to extract the transform-version bits of the flags byte.</summary>
    private const int TransformVersionShift = 6;

    /// <summary>Known-tag index of the <c>head</c> table.</summary>
    private const int HeadIndex = 1;

    /// <summary>Known-tag index of the <c>hhea</c> table.</summary>
    private const int HheaIndex = 2;

    /// <summary>Known-tag index of the <c>OS/2</c> table.</summary>
    private const int Os2Index = 6;

    /// <summary>Known-tag index of the <c>glyf</c> table.</summary>
    private const int GlyfIndex = 10;

    /// <summary>Known-tag index of the <c>loca</c> table.</summary>
    private const int LocaIndex = 11;

    /// <summary>The null-transform version for <c>glyf</c>/<c>loca</c> (no <c>transformLength</c> field follows).</summary>
    private const int GlyfLocaNullTransform = 3;

    /// <summary>Maximum number of bytes in a UIntBase128-encoded value.</summary>
    private const int MaxBase128Bytes = 5;

    /// <summary>Number of payload bits in one UIntBase128 byte.</summary>
    private const int Base128PayloadBits = 7;

    /// <summary>Payload mask for one UIntBase128 byte.</summary>
    private const int Base128PayloadMask = 0x7F;

    /// <summary>Continuation-bit mask for one UIntBase128 byte.</summary>
    private const int Base128ContinuationBit = 0x80;

    /// <summary>Mask of the high 7 bits of a <see cref="uint"/>; a non-zero result before a left-shift-by-7 means overflow.</summary>
    private const uint Base128OverflowMask = 0xFE000000;

    /// <summary>Parses the metrics from <paramref name="woff2"/>.</summary>
    /// <param name="woff2">Raw woff2 bytes.</param>
    /// <returns>The metrics, or <see langword="null"/> when the data is malformed, the table block fails to decompress, or a required table is missing/transformed.</returns>
    public static FontMetrics? TryRead(ReadOnlySpan<byte> woff2)
    {
        if (woff2.Length < HeaderSize || BinaryPrimitives.ReadUInt32BigEndian(woff2) != Woff2Signature)
        {
            return null;
        }

        var numTables = BinaryPrimitives.ReadUInt16BigEndian(woff2[NumTablesOffset..]);
        var totalCompressedSize = BinaryPrimitives.ReadUInt32BigEndian(woff2[TotalCompressedSizeOffset..]);
        var tables = new Woff2Table[numTables];
        if (!TryReadDirectory(woff2, tables, out var directoryEnd, out var blockSize) ||
            totalCompressedSize > (uint)(woff2.Length - directoryEnd))
        {
            return null;
        }

        var rented = ArrayPool<byte>.Shared.Rent(blockSize == 0 ? 1 : blockSize);
        try
        {
            var block = rented.AsSpan(0, blockSize);
            return BrotliDecoder.TryDecompress(woff2.Slice(directoryEnd, (int)totalCompressedSize), block, out var written) && written >= blockSize
                ? ReconstructAndRead(block, tables)
                : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    /// <summary>Parses the whole table directory, computing each table's offset within the decompressed block.</summary>
    /// <param name="woff2">Raw woff2 bytes.</param>
    /// <param name="tables">Destination array (length = number of tables).</param>
    /// <param name="directoryEnd">On success, the byte offset just past the directory.</param>
    /// <param name="blockSize">On success, the total size of the decompressed block.</param>
    /// <returns><see langword="true"/> on success.</returns>
    private static bool TryReadDirectory(ReadOnlySpan<byte> woff2, Woff2Table[] tables, out int directoryEnd, out int blockSize)
    {
        var pos = HeaderSize;
        var offset = 0;
        for (var i = 0; i < tables.Length; i++)
        {
            if (!TryReadDirectoryEntry(woff2, ref pos, offset, out tables[i]))
            {
                directoryEnd = 0;
                blockSize = 0;
                return false;
            }

            offset += tables[i].OnDiskLength;
        }

        directoryEnd = pos;
        blockSize = offset;
        return true;
    }

    /// <summary>Reads one table-directory entry, advancing <paramref name="pos"/> past it.</summary>
    /// <param name="woff2">Raw woff2 bytes.</param>
    /// <param name="pos">Cursor; advanced past the entry.</param>
    /// <param name="blockOffset">This table's offset within the decompressed block.</param>
    /// <param name="table">On success, the parsed entry.</param>
    /// <returns><see langword="true"/> on success.</returns>
    private static bool TryReadDirectoryEntry(ReadOnlySpan<byte> woff2, ref int pos, int blockOffset, out Woff2Table table)
    {
        table = default;
        if (pos >= woff2.Length)
        {
            return false;
        }

        var flags = woff2[pos++];
        var knownIndex = flags & KnownIndexMask;
        var transformVersion = flags >> TransformVersionShift;
        if (knownIndex == ArbitraryTagIndex)
        {
            pos += ArbitraryTagSize;
        }

        if (pos > woff2.Length || !TryReadUIntBase128(woff2, ref pos, out var originalLength))
        {
            return false;
        }

        var transformed = knownIndex is GlyfIndex or LocaIndex ? transformVersion != GlyfLocaNullTransform : transformVersion != 0;
        var onDiskLength = originalLength;
        if (transformed && !TryReadUIntBase128(woff2, ref pos, out onDiskLength))
        {
            return false;
        }

        table = new(knownIndex, blockOffset, (int)originalLength, (int)onDiskLength, transformed);
        return true;
    }

    /// <summary>Builds a minimal sfnt from the <c>head</c>/<c>hhea</c>/<c>OS/2</c> slices of <paramref name="block"/> and reads its metrics.</summary>
    /// <param name="block">Decompressed table block.</param>
    /// <param name="tables">Parsed table directory.</param>
    /// <returns>The metrics, or <see langword="null"/> when a required table is missing or transformed.</returns>
    private static FontMetrics? ReconstructAndRead(ReadOnlySpan<byte> block, Woff2Table[] tables)
    {
        Span<(uint Tag, int Offset, int Length)> slices = stackalloc (uint, int, int)[3];
        var found = 0;
        var hasHead = false;
        var hasHhea = false;
        AddSlice(block, tables, HeadIndex, HeadTag, slices, ref found, ref hasHead);
        AddSlice(block, tables, HheaIndex, HheaTag, slices, ref found, ref hasHhea);
        var ignored = false;
        AddSlice(block, tables, Os2Index, Os2Tag, slices, ref found, ref ignored);
        if (!hasHead || !hasHhea)
        {
            return null;
        }

        return SfntTableReader.TryRead(BuildSfnt(block, slices[..found]));
    }

    /// <summary>Appends the slice for the given known-tag index to <paramref name="slices"/> when present and untransformed.</summary>
    /// <param name="block">Decompressed table block.</param>
    /// <param name="tables">Parsed table directory.</param>
    /// <param name="knownIndex">Known-tag index to look for.</param>
    /// <param name="tag">Big-endian tag to record.</param>
    /// <param name="slices">Destination slice list.</param>
    /// <param name="found">Running count of recorded slices.</param>
    /// <param name="present">Set to <see langword="true"/> when the slice was added.</param>
    private static void AddSlice(ReadOnlySpan<byte> block, Woff2Table[] tables, int knownIndex, uint tag, Span<(uint Tag, int Offset, int Length)> slices, ref int found, ref bool present)
    {
        for (var i = 0; i < tables.Length; i++)
        {
            if (tables[i].KnownIndex != knownIndex || tables[i].Transformed)
            {
                continue;
            }

            if (tables[i].Offset + tables[i].OriginalLength > block.Length)
            {
                return;
            }

            slices[found++] = (tag, tables[i].Offset, tables[i].OriginalLength);
            present = true;
            return;
        }
    }

    /// <summary>Assembles a minimal sfnt blob from the given table slices.</summary>
    /// <param name="block">Decompressed table block.</param>
    /// <param name="slices">The tables to include (tag, offset within <paramref name="block"/>, length).</param>
    /// <returns>The sfnt bytes.</returns>
    private static byte[] BuildSfnt(ReadOnlySpan<byte> block, ReadOnlySpan<(uint Tag, int Offset, int Length)> slices)
    {
        var size = SfntOffsetTableSize + (slices.Length * SfntRecordSize);
        for (var i = 0; i < slices.Length; i++)
        {
            size += slices[i].Length;
        }

        var sfnt = new byte[size];
        BinaryPrimitives.WriteUInt32BigEndian(sfnt.AsSpan(0), SfntVersionTrueType);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(SfntNumTablesOffset), (ushort)slices.Length);
        var bodyOffset = SfntOffsetTableSize + (slices.Length * SfntRecordSize);
        for (var i = 0; i < slices.Length; i++)
        {
            var rec = sfnt.AsSpan(SfntOffsetTableSize + (i * SfntRecordSize));
            BinaryPrimitives.WriteUInt32BigEndian(rec, slices[i].Tag);
            BinaryPrimitives.WriteUInt32BigEndian(rec[RecordOffsetField..], (uint)bodyOffset);
            BinaryPrimitives.WriteUInt32BigEndian(rec[RecordLengthField..], (uint)slices[i].Length);
            block.Slice(slices[i].Offset, slices[i].Length).CopyTo(sfnt.AsSpan(bodyOffset));
            bodyOffset += slices[i].Length;
        }

        return sfnt;
    }

    /// <summary>Reads a woff2 UIntBase128 value (big-endian, 7 bits per byte, high bit = continuation, at most 5 bytes, no leading-zero byte).</summary>
    /// <param name="data">Source bytes.</param>
    /// <param name="pos">Cursor; advanced past the value.</param>
    /// <param name="value">The decoded value.</param>
    /// <returns><see langword="true"/> on success.</returns>
    private static bool TryReadUIntBase128(ReadOnlySpan<byte> data, ref int pos, out uint value)
    {
        value = 0;
        for (var i = 0; i < MaxBase128Bytes; i++)
        {
            if (pos >= data.Length)
            {
                return false;
            }

            var b = data[pos++];
            if ((i == 0 && b == Base128ContinuationBit) || (value & Base128OverflowMask) != 0)
            {
                return false;
            }

            value = (value << Base128PayloadBits) | (uint)(b & Base128PayloadMask);
            if ((b & Base128ContinuationBit) == 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>One parsed woff2 table-directory entry.</summary>
    /// <param name="KnownIndex">Known-tag index, or 63 for an arbitrary tag.</param>
    /// <param name="Offset">Byte offset of the table within the decompressed block.</param>
    /// <param name="OriginalLength">The table's uncompressed/untransformed length.</param>
    /// <param name="OnDiskLength">The table's length within the decompressed block (transformed length when transformed).</param>
    /// <param name="Transformed">Whether the table is stored in a transformed form.</param>
    private readonly record struct Woff2Table(int KnownIndex, int Offset, int OriginalLength, int OnDiskLength, bool Transformed);
}
