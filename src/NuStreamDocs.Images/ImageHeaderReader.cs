// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace NuStreamDocs.Images;

/// <summary>Reads intrinsic pixel width and height from PNG, GIF, JPEG, WebP, AVIF/HEIF, and SVG headers in pure managed UTF-8 and binary spans.</summary>
public static class ImageHeaderReader
{
    /// <summary>Length of a UTF-8 byte-order mark.</summary>
    private const int Utf8BomLength = 3;

    /// <summary>Minimum byte length required to read a PNG signature and IHDR width/height.</summary>
    private const int PngMinHeaderBytes = 24;

    /// <summary>Offset of the IHDR chunk type inside a PNG file.</summary>
    private const int PngChunkTypeOffset = 12;

    /// <summary>Offset of the 32-bit big-endian width field inside PNG IHDR.</summary>
    private const int PngWidthOffset = 16;

    /// <summary>Offset of the 32-bit big-endian height field inside PNG IHDR.</summary>
    private const int PngHeightOffset = 20;

    /// <summary>Byte width of a 32-bit integer.</summary>
    private const int Int32ByteWidth = 4;

    /// <summary>Byte width of a 16-bit integer.</summary>
    private const int Int16ByteWidth = 2;

    /// <summary>Minimum byte length required to read GIF87a/GIF89a dimensions.</summary>
    private const int GifMinHeaderBytes = 10;

    /// <summary>Offset of the 16-bit little-endian width in a GIF header.</summary>
    private const int GifWidthOffset = 6;

    /// <summary>Offset of the 16-bit little-endian height in a GIF header.</summary>
    private const int GifHeightOffset = 8;

    /// <summary>Minimum byte length required for a RIFF WebP header.</summary>
    private const int WebpMinHeaderBytes = 30;

    /// <summary>Offset of the <c>WEBP</c> four-character code.</summary>
    private const int WebpFourCcOffset = 8;

    /// <summary>Offset of the inner WebP chunk type (<c>VP8 </c>, <c>VP8L</c>, or <c>VP8X</c>).</summary>
    private const int WebpChunkOffset = 12;

    /// <summary>Offset of the VP8 keyframe start code (<c>9D 01 2A</c>).</summary>
    private const int Vp8StartCodeOffset = 23;

    /// <summary>Offset of the 14-bit VP8 width field.</summary>
    private const int Vp8WidthOffset = 26;

    /// <summary>Offset of the 14-bit VP8 height field.</summary>
    private const int Vp8HeightOffset = 28;

    /// <summary>14-bit dimension mask for VP8 and VP8L headers.</summary>
    private const int FourteenBitMask = 0x3FFF;

    /// <summary>Bit shift for the packed height in a VP8L header.</summary>
    private const int Vp8LHeightShift = 14;

    /// <summary>Offset of the VP8L <c>0x2F</c> signature byte.</summary>
    private const int Vp8LSignatureOffset = 20;

    /// <summary>Expected VP8L signature byte.</summary>
    private const byte Vp8LSignatureByte = 0x2F;

    /// <summary>Offset of the 24-bit width-minus-one field in a VP8X header.</summary>
    private const int Vp8XWidthOffset = 24;

    /// <summary>Offset of the 24-bit height-minus-one field in a VP8X header.</summary>
    private const int Vp8XHeightOffset = 27;

    /// <summary>Bit shift for the middle byte of a 24-bit little-endian integer.</summary>
    private const int ByteShift8 = 8;

    /// <summary>Bit shift for the high byte of a 24-bit little-endian integer.</summary>
    private const int ByteShift16 = 16;

    /// <summary>JPEG marker prefix byte.</summary>
    private const byte JpegMarkerPrefix = 0xFF;

    /// <summary>Minimum segment payload length for a JPEG SOF frame header.</summary>
    private const int JpegSofMinLength = 7;

    /// <summary>Offset of the 16-bit height inside a JPEG SOF segment.</summary>
    private const int JpegSofHeightOffset = 3;

    /// <summary>Offset of the 16-bit width inside a JPEG SOF segment.</summary>
    private const int JpegSofWidthOffset = 5;

    /// <summary>Minimum byte length for an ISO BMFF (<c>ftyp</c>) container header.</summary>
    private const int IsoBmffMinBytes = 16;

    /// <summary>Byte offset from the start of <c>ispe</c> to the 32-bit width field (<c>ispe</c> + 4 version/flags bytes).</summary>
    private const int IspeWidthDelta = 8;

    /// <summary>Byte offset from the start of <c>ispe</c> to the 32-bit height field.</summary>
    private const int IspeHeightDelta = 12;

    /// <summary>Total bytes required from <c>ispe</c> tag start to the end of height.</summary>
    private const int IspeRequiredTail = 16;

    /// <summary>Number of numeric tokens in an SVG <c>viewBox</c> attribute.</summary>
    private const int SvgViewBoxTokenCount = 4;

    /// <summary>Index of width in a 4-value SVG <c>viewBox</c>.</summary>
    private const int SvgViewBoxWidthIndex = 2;

    /// <summary>Index of height in a 4-value SVG <c>viewBox</c>.</summary>
    private const int SvgViewBoxHeightIndex = 3;

    /// <summary>Maximum bytes scanned at the start of an SVG file to locate the root <c>&lt;svg&gt;</c> tag.</summary>
    private const int SvgScanLimitBytes = 4096;

    /// <summary>ASCII whitespace characters allowed before an SVG attribute name.</summary>
    private static readonly SearchValues<byte> SvgAttrLeadingWhitespace = SearchValues.Create(" \t\r\n"u8);

    /// <summary>Attempts to parse intrinsic width and height from the leading bytes of an image file.</summary>
    /// <param name="data">Raw file bytes (or header prefix).</param>
    /// <param name="dimensions">Parsed positive pixel dimensions on success.</param>
    /// <returns><c>true</c> when a supported format with valid positive dimensions was recognized.</returns>
    public static bool TryReadDimensions(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        ReadOnlySpan<byte> utf8Bom = [0xEF, 0xBB, 0xBF];
        var stripped = data.StartsWith(utf8Bom) ? data[Utf8BomLength..] : data;
        if (TryReadPng(stripped, out dimensions)
            || TryReadGif(stripped, out dimensions)
            || TryReadWebp(stripped, out dimensions)
            || TryReadJpeg(stripped, out dimensions)
            || TryReadIsoBmffIspe(stripped, out dimensions))
        {
            return true;
        }

        return TryReadSvg(stripped, out dimensions);
    }

    /// <summary>Parses PNG IHDR width and height.</summary>
    /// <param name="data">Image bytes.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> on valid PNG IHDR.</returns>
    private static bool TryReadPng(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        ReadOnlySpan<byte> pngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        if (data.Length < PngMinHeaderBytes
            || !data.StartsWith(pngSignature)
            || !data.Slice(PngChunkTypeOffset, Int32ByteWidth).SequenceEqual("IHDR"u8))
        {
            dimensions = default;
            return false;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(PngWidthOffset, Int32ByteWidth));
        var height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(PngHeightOffset, Int32ByteWidth));
        return ValidateDimensions(width, height, out dimensions);
    }

    /// <summary>Parses GIF87a and GIF89a logical screen dimensions.</summary>
    /// <param name="data">Image bytes.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> on valid GIF header.</returns>
    private static bool TryReadGif(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        if (data.Length < GifMinHeaderBytes
            || (!data.StartsWith("GIF89a"u8) && !data.StartsWith("GIF87a"u8)))
        {
            dimensions = default;
            return false;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(GifWidthOffset, Int16ByteWidth));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(GifHeightOffset, Int16ByteWidth));
        return ValidateDimensions(width, height, out dimensions);
    }

    /// <summary>Parses RIFF WebP (<c>VP8 </c>, <c>VP8L</c>, and <c>VP8X</c>) dimensions.</summary>
    /// <param name="data">Image bytes.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> on valid WebP header.</returns>
    private static bool TryReadWebp(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        if (data.Length < WebpMinHeaderBytes
            || !data.StartsWith("RIFF"u8)
            || !data.Slice(WebpFourCcOffset, Int32ByteWidth).SequenceEqual("WEBP"u8))
        {
            dimensions = default;
            return false;
        }

        var chunk = data.Slice(WebpChunkOffset, Int32ByteWidth);
        if (chunk.SequenceEqual("VP8 "u8))
        {
            return TryReadVp8Lossy(data, out dimensions);
        }

        if (chunk.SequenceEqual("VP8L"u8))
        {
            return TryReadVp8Lossless(data, out dimensions);
        }

        if (chunk.SequenceEqual("VP8X"u8))
        {
            var width = 1 + ReadUInt24LittleEndian(data[Vp8XWidthOffset..]);
            var height = 1 + ReadUInt24LittleEndian(data[Vp8XHeightOffset..]);
            return ValidateDimensions(width, height, out dimensions);
        }

        dimensions = default;
        return false;
    }

    /// <summary>Parses a lossy VP8 keyframe header.</summary>
    /// <param name="data">WebP bytes.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> when the VP8 start code is valid.</returns>
    private static bool TryReadVp8Lossy(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        ReadOnlySpan<byte> startCode = [0x9D, 0x01, 0x2A];
        if (!data.Slice(Vp8StartCodeOffset, startCode.Length).SequenceEqual(startCode))
        {
            dimensions = default;
            return false;
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(Vp8WidthOffset, Int16ByteWidth)) & FourteenBitMask;
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(Vp8HeightOffset, Int16ByteWidth)) & FourteenBitMask;
        return ValidateDimensions(width, height, out dimensions);
    }

    /// <summary>Parses a lossless VP8L bitstream header.</summary>
    /// <param name="data">WebP bytes.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> when the VP8L signature byte is valid.</returns>
    private static bool TryReadVp8Lossless(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        if (data[Vp8LSignatureOffset] != Vp8LSignatureByte)
        {
            dimensions = default;
            return false;
        }

        var packed = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(Vp8LSignatureOffset + 1, Int32ByteWidth));
        var width = 1 + (int)(packed & FourteenBitMask);
        var height = 1 + (int)((packed >> Vp8LHeightShift) & FourteenBitMask);
        return ValidateDimensions(width, height, out dimensions);
    }

    /// <summary>Reads a 24-bit unsigned little-endian integer.</summary>
    /// <param name="bytes">At least 3 bytes.</param>
    /// <returns>24-bit integer value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadUInt24LittleEndian(ReadOnlySpan<byte> bytes) =>
        bytes[0] | (bytes[1] << ByteShift8) | (bytes[Int16ByteWidth] << ByteShift16);

    /// <summary>Walks JPEG markers to locate the Start of Frame (<c>SOF0..SOF3</c>) dimensions.</summary>
    /// <param name="data">Image bytes.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> when a valid SOF marker was found.</returns>
    private static bool TryReadJpeg(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        dimensions = default;
        if (data.Length < Int32ByteWidth || data[0] != JpegMarkerPrefix || data[1] != 0xD8)
        {
            return false;
        }

        var pos = Int16ByteWidth;
        while (TryReadNextJpegMarker(data, ref pos, out var marker))
        {
            if (IsStandaloneJpegMarker(marker))
            {
                continue;
            }

            if (pos + Int16ByteWidth > data.Length)
            {
                return false;
            }

            var segLength = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(pos, Int16ByteWidth));
            if (segLength < Int16ByteWidth || pos + segLength > data.Length)
            {
                return false;
            }

            if (IsJpegSofMarker(marker))
            {
                return TryReadJpegSofSegment(data.Slice(pos, segLength), out dimensions);
            }

            pos += segLength;
        }

        return false;
    }

    /// <summary>Advances past <c>0xFF</c> marker prefix bytes and reads the marker identifier.</summary>
    /// <param name="data">JPEG byte stream.</param>
    /// <param name="pos">Current stream offset.</param>
    /// <param name="marker">Marker byte following <c>0xFF</c>.</param>
    /// <returns><c>true</c> when a marker byte was read.</returns>
    private static bool TryReadNextJpegMarker(ReadOnlySpan<byte> data, ref int pos, out byte marker)
    {
        marker = 0;
        if (pos >= data.Length || data[pos] != JpegMarkerPrefix)
        {
            return false;
        }

        while (pos < data.Length && data[pos] == JpegMarkerPrefix)
        {
            pos++;
        }

        if (pos >= data.Length)
        {
            return false;
        }

        marker = data[pos];
        pos++;
        return true;
    }

    /// <summary>Reads width and height from a JPEG SOF payload segment.</summary>
    /// <param name="segment">SOF segment starting at its 2-byte length field.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> when segment length and dimensions are valid.</returns>
    private static bool TryReadJpegSofSegment(ReadOnlySpan<byte> segment, out ImageDimensions dimensions)
    {
        if (segment.Length < JpegSofMinLength)
        {
            dimensions = default;
            return false;
        }

        var height = BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(JpegSofHeightOffset, Int16ByteWidth));
        var width = BinaryPrimitives.ReadUInt16BigEndian(segment.Slice(JpegSofWidthOffset, Int16ByteWidth));
        return ValidateDimensions(width, height, out dimensions);
    }

    /// <summary>Checks whether a JPEG marker carries no length payload.</summary>
    /// <param name="marker">Marker byte after <c>0xFF</c>.</param>
    /// <returns><c>true</c> for TEM, RST0..RST7, SOI, and EOI.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsStandaloneJpegMarker(byte marker) =>
        marker is 0x01 or 0xD8 or 0xD9 or (>= 0xD0 and <= 0xD7);

    /// <summary>Checks whether a JPEG marker is a Start of Frame marker carrying dimensions.</summary>
    /// <param name="marker">Marker byte.</param>
    /// <returns><c>true</c> for baseline, extended, progressive, and lossless SOF markers.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsJpegSofMarker(byte marker) =>
        marker is 0xC0 or 0xC1 or 0xC2 or 0xC3;

    /// <summary>Parses AVIF / HEIF ISO BMFF <c>ispe</c> image spatial extents.</summary>
    /// <param name="data">Image bytes.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> when an <c>ftyp</c> header and valid <c>ispe</c> box were found.</returns>
    private static bool TryReadIsoBmffIspe(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        dimensions = default;
        if (data.Length < IsoBmffMinBytes || !data.Slice(Int32ByteWidth, Int32ByteWidth).SequenceEqual("ftyp"u8))
        {
            return false;
        }

        var ispeIdx = data.IndexOf("ispe"u8);
        if (ispeIdx < 0 || ispeIdx + IspeRequiredTail > data.Length)
        {
            return false;
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(ispeIdx + IspeWidthDelta, Int32ByteWidth));
        var height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(ispeIdx + IspeHeightDelta, Int32ByteWidth));
        return ValidateDimensions(width, height, out dimensions);
    }

    /// <summary>Parses root <c>&lt;svg&gt;</c> <c>width</c>/<c>height</c> attributes or <c>viewBox</c> fallback.</summary>
    /// <param name="data">UTF-8 SVG bytes.</param>
    /// <param name="dimensions">Parsed rounded pixel dimensions.</param>
    /// <returns><c>true</c> when positive dimensions were found.</returns>
    private static bool TryReadSvg(ReadOnlySpan<byte> data, out ImageDimensions dimensions)
    {
        dimensions = default;
        var head = data.Length > SvgScanLimitBytes ? data[..SvgScanLimitBytes] : data;
        var svgStart = head.IndexOf("<svg"u8);
        if (svgStart < 0)
        {
            return false;
        }

        var afterSvg = head[svgStart..];
        var tagEnd = afterSvg.IndexOf((byte)'>');
        if (tagEnd < 0)
        {
            return false;
        }

        var attrs = afterSvg[..tagEnd];
        return TryReadSvgLengthAttr(attrs, "width"u8, out var width)
            && TryReadSvgLengthAttr(attrs, "height"u8, out var height)
            ? ValidateDimensions(width, height, out dimensions)
            : TryReadSvgViewBox(attrs, out dimensions);
    }

    /// <summary>Reads a pixel-valued <c>width</c> or <c>height</c> attribute (ignoring percentage values like <c>100%</c>) from an <c>&lt;svg&gt;</c> tag.</summary>
    /// <param name="attrs">SVG opening tag bytes.</param>
    /// <param name="attrName">Attribute name (<c>width</c> or <c>height</c>).</param>
    /// <param name="pixels">Rounded integer pixel length.</param>
    /// <returns><c>true</c> when a numeric non-percentage value was parsed.</returns>
    private static bool TryReadSvgLengthAttr(ReadOnlySpan<byte> attrs, ReadOnlySpan<byte> attrName, out int pixels)
    {
        pixels = 0;
        if (!TryExtractQuotedAttr(attrs, attrName, out var raw) || raw.EndsWith("%"u8))
        {
            return false;
        }

        var trimmed = raw.EndsWith("px"u8) ? raw[..^Int16ByteWidth] : raw;
        if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var num) || num <= 0)
        {
            return false;
        }

        pixels = (int)Math.Round(num, MidpointRounding.AwayFromZero);
        return pixels > 0;
    }

    /// <summary>Parses the 3rd and 4th numbers of an SVG <c>viewBox="minX minY width height"</c> attribute.</summary>
    /// <param name="attrs">SVG opening tag bytes.</param>
    /// <param name="dimensions">Parsed dimensions.</param>
    /// <returns><c>true</c> when 4 numeric tokens with positive width and height were found.</returns>
    private static bool TryReadSvgViewBox(ReadOnlySpan<byte> attrs, out ImageDimensions dimensions)
    {
        dimensions = default;
        if (!TryExtractQuotedAttr(attrs, "viewBox"u8, out var raw))
        {
            return false;
        }

        Span<double> values = stackalloc double[SvgViewBoxTokenCount];
        var cursor = 0;
        for (var i = 0; i < SvgViewBoxTokenCount; i++)
        {
            if (!TryReadNextViewBoxNumber(raw, ref cursor, out values[i]))
            {
                return false;
            }
        }

        var width = (int)Math.Round(values[SvgViewBoxWidthIndex], MidpointRounding.AwayFromZero);
        var height = (int)Math.Round(values[SvgViewBoxHeightIndex], MidpointRounding.AwayFromZero);
        return ValidateDimensions(width, height, out dimensions);
    }

    /// <summary>Reads the next comma- or whitespace-separated floating-point token from <paramref name="raw"/>.</summary>
    /// <param name="raw">ViewBox attribute value.</param>
    /// <param name="cursor">Current byte cursor.</param>
    /// <param name="number">Parsed number.</param>
    /// <returns><c>true</c> when a float token was parsed.</returns>
    private static bool TryReadNextViewBoxNumber(ReadOnlySpan<byte> raw, ref int cursor, out double number)
    {
        while (cursor < raw.Length && IsViewBoxSeparator(raw[cursor]))
        {
            cursor++;
        }

        var start = cursor;
        while (cursor < raw.Length && !IsViewBoxSeparator(raw[cursor]))
        {
            cursor++;
        }

        if (start == cursor)
        {
            number = 0;
            return false;
        }

        return double.TryParse(raw[start..cursor], NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    /// <summary>Checks whether <paramref name="b"/> separates SVG <c>viewBox</c> numbers.</summary>
    /// <param name="b">Candidate byte.</param>
    /// <returns><c>true</c> for space, tab, or comma.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsViewBoxSeparator(byte b) =>
        b is (byte)' ' or (byte)'\t' or (byte)',';

    /// <summary>Finds a standalone <c>name="value"</c> or <c>name='value'</c> attribute on an SVG tag (skipping hyphenated prefixes like <c>stroke-width</c>).</summary>
    /// <param name="attrs">Tag bytes.</param>
    /// <param name="name">Attribute name.</param>
    /// <param name="value">Extracted attribute value.</param>
    /// <returns><c>true</c> when matched.</returns>
    private static bool TryExtractQuotedAttr(ReadOnlySpan<byte> attrs, ReadOnlySpan<byte> name, out ReadOnlySpan<byte> value)
    {
        var searchFrom = 0;
        while (searchFrom < attrs.Length)
        {
            var rel = attrs[searchFrom..].IndexOf(name);
            if (rel < 0)
            {
                break;
            }

            var pos = searchFrom + rel;
            if (SvgAttrLeadingWhitespace.Contains(attrs[pos - 1])
                && TryReadAfterAttrName(attrs, pos + name.Length, out value))
            {
                return true;
            }

            searchFrom = pos + name.Length;
        }

        value = default;
        return false;
    }

    /// <summary>Reads <c>="value"</c> or <c>='value'</c> starting immediately past an attribute name.</summary>
    /// <param name="attrs">Tag bytes.</param>
    /// <param name="afterName">Offset past attribute name.</param>
    /// <param name="value">Quoted value slice.</param>
    /// <returns><c>true</c> when matched.</returns>
    private static bool TryReadAfterAttrName(ReadOnlySpan<byte> attrs, int afterName, out ReadOnlySpan<byte> value)
    {
        value = default;
        if (afterName + Int16ByteWidth >= attrs.Length || attrs[afterName] != (byte)'=')
        {
            return false;
        }

        var quote = attrs[afterName + 1];
        if (quote is not ((byte)'"' or (byte)'\''))
        {
            return false;
        }

        var valStart = afterName + Int16ByteWidth;
        var closeRel = attrs[valStart..].IndexOf(quote);
        if (closeRel < 0)
        {
            return false;
        }

        value = attrs.Slice(valStart, closeRel).Trim(" \t"u8);
        return true;
    }

    /// <summary>Checks that both <paramref name="width"/> and <paramref name="height"/> are positive.</summary>
    /// <param name="width">Candidate width.</param>
    /// <param name="height">Candidate height.</param>
    /// <param name="dimensions">Validated struct.</param>
    /// <returns><c>true</c> when both dimensions exceed zero.</returns>
    private static bool ValidateDimensions(int width, int height, out ImageDimensions dimensions)
    {
        if (width > 0 && height > 0)
        {
            dimensions = new(width, height);
            return true;
        }

        dimensions = default;
        return false;
    }
}
