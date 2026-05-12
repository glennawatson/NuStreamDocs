// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace NuStreamDocs.Fonts;

/// <summary>Reads the vertical metrics out of an sfnt-wrapped (ttf/otf) font's <c>head</c>, <c>hhea</c>, and <c>OS/2</c> tables.</summary>
public static class SfntTableReader
{
    /// <summary>Size in bytes of the sfnt offset table that precedes the table records.</summary>
    private const int OffsetTableSize = 12;

    /// <summary>Size in bytes of one entry in the sfnt table directory.</summary>
    private const int TableRecordSize = 16;

    /// <summary>Size in bytes of a 16-bit field.</summary>
    private const int Uint16Size = 2;

    /// <summary>Tag <c>head</c>.</summary>
    private const uint HeadTag = 0x68656164;

    /// <summary>Tag <c>hhea</c>.</summary>
    private const uint HheaTag = 0x68686561;

    /// <summary>Tag <c>OS/2</c>.</summary>
    private const uint Os2Tag = 0x4F532F32;

    /// <summary>Offset of <c>unitsPerEm</c> within the <c>head</c> table.</summary>
    private const int HeadUnitsPerEmOffset = 18;

    /// <summary>Offset of <c>ascender</c> within the <c>hhea</c> table.</summary>
    private const int HheaAscenderOffset = 4;

    /// <summary>Offset of <c>descender</c> within the <c>hhea</c> table.</summary>
    private const int HheaDescenderOffset = 6;

    /// <summary>Offset of <c>lineGap</c> within the <c>hhea</c> table.</summary>
    private const int HheaLineGapOffset = 8;

    /// <summary>Offset of <c>sxHeight</c> within an <c>OS/2</c> table of version 2 or later.</summary>
    private const int Os2XHeightOffset = 86;

    /// <summary>Offset of <c>sCapHeight</c> within an <c>OS/2</c> table of version 2 or later.</summary>
    private const int Os2CapHeightOffset = 88;

    /// <summary>Minimum <c>OS/2</c> table version that includes <c>sxHeight</c> / <c>sCapHeight</c>.</summary>
    private const int Os2MetricsMinVersion = 2;

    /// <summary>Parses the metrics from <paramref name="sfnt"/>.</summary>
    /// <param name="sfnt">Raw ttf/otf bytes.</param>
    /// <returns>The metrics, or <see langword="null"/> when the data is too short, malformed, or missing a required table.</returns>
    public static FontMetrics? TryRead(ReadOnlySpan<byte> sfnt)
    {
        if (sfnt.Length < OffsetTableSize)
        {
            return null;
        }

        var numTables = BinaryPrimitives.ReadUInt16BigEndian(sfnt[4..]);
        if (sfnt.Length < OffsetTableSize + (numTables * TableRecordSize))
        {
            return null;
        }

        if (!TryFindTable(sfnt, numTables, HeadTag, out var headStart, out var headLen) || headLen < HeadUnitsPerEmOffset + Uint16Size)
        {
            return null;
        }

        if (!TryFindTable(sfnt, numTables, HheaTag, out var hheaStart, out var hheaLen) || hheaLen < HheaLineGapOffset + Uint16Size)
        {
            return null;
        }

        var unitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(sfnt.Slice(headStart + HeadUnitsPerEmOffset));
        if (unitsPerEm == 0)
        {
            return null;
        }

        var ascender = BinaryPrimitives.ReadInt16BigEndian(sfnt.Slice(hheaStart + HheaAscenderOffset));
        var descender = BinaryPrimitives.ReadInt16BigEndian(sfnt.Slice(hheaStart + HheaDescenderOffset));
        var lineGap = BinaryPrimitives.ReadInt16BigEndian(sfnt.Slice(hheaStart + HheaLineGapOffset));
        var (xHeight, capHeight) = ReadOs2Heights(sfnt, numTables);
        return new(unitsPerEm, ascender, descender, lineGap, xHeight, capHeight);
    }

    /// <summary>Reads <c>sxHeight</c> / <c>sCapHeight</c> from an <c>OS/2</c> v2+ table; returns zeroes when the table is absent or too old.</summary>
    /// <param name="sfnt">Raw sfnt bytes.</param>
    /// <param name="numTables">Number of table records.</param>
    /// <returns>The x-height and cap-height, or <c>(0, 0)</c>.</returns>
    private static (int XHeight, int CapHeight) ReadOs2Heights(ReadOnlySpan<byte> sfnt, int numTables)
    {
        if (!TryFindTable(sfnt, numTables, Os2Tag, out var os2Start, out var os2Len) || os2Len < Os2CapHeightOffset + Uint16Size)
        {
            return (0, 0);
        }

        if (BinaryPrimitives.ReadUInt16BigEndian(sfnt.Slice(os2Start)) < Os2MetricsMinVersion)
        {
            return (0, 0);
        }

        return (
            BinaryPrimitives.ReadInt16BigEndian(sfnt.Slice(os2Start + Os2XHeightOffset)),
            BinaryPrimitives.ReadInt16BigEndian(sfnt.Slice(os2Start + Os2CapHeightOffset)));
    }

    /// <summary>Walks the table directory looking for <paramref name="tag"/>.</summary>
    /// <param name="sfnt">Raw sfnt bytes.</param>
    /// <param name="numTables">Number of table records.</param>
    /// <param name="tag">Big-endian table tag to find.</param>
    /// <param name="start">On success, the table's byte offset within <paramref name="sfnt"/>.</param>
    /// <param name="length">On success, the table's byte length.</param>
    /// <returns><see langword="true"/> when the table is present and its slice is within bounds.</returns>
    private static bool TryFindTable(ReadOnlySpan<byte> sfnt, int numTables, uint tag, out int start, out int length)
    {
        for (var i = 0; i < numTables; i++)
        {
            var record = sfnt.Slice(OffsetTableSize + (i * TableRecordSize), TableRecordSize);
            if (BinaryPrimitives.ReadUInt32BigEndian(record) != tag)
            {
                continue;
            }

            var offset = BinaryPrimitives.ReadUInt32BigEndian(record[8..]);
            var len = BinaryPrimitives.ReadUInt32BigEndian(record[12..]);
            if (offset > (uint)sfnt.Length || len > (uint)sfnt.Length - offset)
            {
                break;
            }

            start = (int)offset;
            length = (int)len;
            return true;
        }

        start = 0;
        length = 0;
        return false;
    }
}
