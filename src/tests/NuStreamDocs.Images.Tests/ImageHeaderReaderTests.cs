// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Images.Tests;

/// <summary>Unit tests for <see cref="ImageHeaderReader"/> across PNG, GIF, WebP, JPEG, AVIF, and SVG headers.</summary>
public sealed class ImageHeaderReaderTests
{
    /// <summary>Expected sample width.</summary>
    private const int SampleWidth = 640;

    /// <summary>Expected sample height.</summary>
    private const int SampleHeight = 360;

    /// <summary>Small test width.</summary>
    private const int SmallWidth = 10;

    /// <summary>Small test height.</summary>
    private const int SmallHeight = 20;

    /// <summary>Buffer length exceeding the 4096-byte SVG scan window.</summary>
    private const int OversizedSvgPrefix = 5000;

    /// <summary>Offset of the VP8 start code byte mutated in invalid-header test.</summary>
    private const int Vp8MutateIndex = 23;

    /// <summary>Offset of the VP8L signature byte mutated in invalid-header test.</summary>
    private const int Vp8LMutateIndex = 20;

    /// <summary>Expected sample dimensions.</summary>
    private static readonly ImageDimensions ExpectedSample = new(SampleWidth, SampleHeight);

    /// <summary>Expected small dimensions.</summary>
    private static readonly ImageDimensions ExpectedSmall = new(SmallWidth, SmallHeight);

    /// <summary>Gets the AVIF <c>ftypavif</c> marker bytes.</summary>
    private static ReadOnlySpan<byte> FtypAvif => "ftypavif"u8;

    /// <summary>Gets the RIFF WebP prefix bytes.</summary>
    private static ReadOnlySpan<byte> RiffWebp => "RIFF\0\0\0\0WEBP"u8;

    /// <summary>Verifies PNG IHDR parsing and malformed PNG rejection.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryReadDimensions_PngValidZeroAndMalformed_BehavesAsExpected()
    {
        byte[] validPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, .. "IHDR"u8, 0, 0, 0x02, 0x80, 0, 0, 0x01, 0x68];
        await Assert.That(ImageHeaderReader.TryReadDimensions(validPng, out var dims)).IsTrue();
        await Assert.That(dims).IsEqualTo(ExpectedSample);

        byte[] zeroPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, .. "IHDR"u8, 0, 0, 0, 0, 0, 0, 0x01, 0x68];
        await Assert.That(ImageHeaderReader.TryReadDimensions(zeroPng, out _)).IsFalse();

        byte[] zeroHeightPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, .. "IHDR"u8, 0, 0, 0x02, 0x80, 0, 0, 0, 0];
        await Assert.That(ImageHeaderReader.TryReadDimensions(zeroHeightPng, out _)).IsFalse();

        byte[] badChunkPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, .. "IDAT"u8, 0, 0, 0, 1, 0, 0, 0, 1];
        await Assert.That(ImageHeaderReader.TryReadDimensions(badChunkPng, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions([0x89, 0x50], out _)).IsFalse();
    }

    /// <summary>Verifies GIF87a and GIF89a logical screen dimension parsing.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryReadDimensions_Gif87aAndGif89a_ParsesDimensions()
    {
        byte[] gif89 = [.. "GIF89a"u8, 0x80, 0x02, 0x68, 0x01];
        await Assert.That(ImageHeaderReader.TryReadDimensions(gif89, out var dims89)).IsTrue();
        await Assert.That(dims89).IsEqualTo(ExpectedSample);

        byte[] gif87 = [.. "GIF87a"u8, 0x0A, 0x00, 0x14, 0x00];
        await Assert.That(ImageHeaderReader.TryReadDimensions(gif87, out var dims87)).IsTrue();
        await Assert.That(dims87).IsEqualTo(ExpectedSmall);

        await Assert.That(ImageHeaderReader.TryReadDimensions("GIF00a1234"u8, out _)).IsFalse();
    }

    /// <summary>Verifies WebP lossy (<c>VP8 </c>), lossless (<c>VP8L</c>), extended (<c>VP8X</c>), and invalid chunk handling.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryReadDimensions_WebpVariantsAndInvalidHeaders_ParsesExpectedValues()
    {
        byte[] vp8 = [.. RiffWebp, .. "VP8 \0\0\0\0\0\0\0"u8, 0x9D, 0x01, 0x2A, 0x80, 0x02, 0x68, 0x01];
        await Assert.That(ImageHeaderReader.TryReadDimensions(vp8, out var vp8Dims)).IsTrue();
        await Assert.That(vp8Dims).IsEqualTo(ExpectedSample);

        vp8[Vp8MutateIndex] = 0x00;
        await Assert.That(ImageHeaderReader.TryReadDimensions(vp8, out _)).IsFalse();

        byte[] vp8L = [.. RiffWebp, .. "VP8L\0\0\0\0"u8, 0x2F, 0x7F, 0xC2, 0x59, 0x00, 0, 0, 0, 0, 0];
        await Assert.That(ImageHeaderReader.TryReadDimensions(vp8L, out var vp8LDims)).IsTrue();
        await Assert.That(vp8LDims).IsEqualTo(ExpectedSample);

        vp8L[Vp8LMutateIndex] = 0x00;
        await Assert.That(ImageHeaderReader.TryReadDimensions(vp8L, out _)).IsFalse();

        byte[] vp8X = [.. RiffWebp, .. "VP8X\0\0\0\0\0\0\0\0"u8, 0x7F, 0x02, 0x00, 0x67, 0x01, 0x00];
        await Assert.That(ImageHeaderReader.TryReadDimensions(vp8X, out var vp8XDims)).IsTrue();
        await Assert.That(vp8XDims).IsEqualTo(ExpectedSample);

        byte[] unknownWebpChunk = [.. "RIFF\0\0\0\0WEBPVP8?\0\0\0\0\0\0\0\0\0\0\0\0\0\0"u8];
        await Assert.That(ImageHeaderReader.TryReadDimensions(unknownWebpChunk, out _)).IsFalse();

        byte[] riffWave = [.. "RIFF\0\0\0\0WAVEfmt \0\0\0\0\0\0\0\0\0\0\0\0\0\0"u8];
        await Assert.That(ImageHeaderReader.TryReadDimensions(riffWave, out _)).IsFalse();
    }

    /// <summary>Verifies JPEG marker walking across padding bytes, standalone markers, APP segments, SOF0..SOF3, and truncated inputs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryReadDimensions_JpegMarkersAndCorruptStreams_CoversAllMarkerBranches()
    {
        byte[] validJpeg =
        [
            0xFF, 0xD8,
            0xFF, 0xFF, 0x01,
            0xFF, 0xD0,
            0xFF, 0xD8,
            0xFF, 0xD9,
            0xFF, 0xE0, 0x00, 0x04, 0x00, 0x00,
            0xFF, 0xC2, 0x00, 0x07, 0x08, 0x01, 0x68, 0x02, 0x80
        ];
        await Assert.That(ImageHeaderReader.TryReadDimensions(validJpeg, out var jpegDims)).IsTrue();
        await Assert.That(jpegDims).IsEqualTo(ExpectedSample);

        byte[] sof0 = [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x07, 0x08, 0x00, 0x0A, 0x00, 0x14];
        await Assert.That(ImageHeaderReader.TryReadDimensions(sof0, out _)).IsTrue();

        byte[] sof1 = [0xFF, 0xD8, 0xFF, 0xC1, 0x00, 0x07, 0x08, 0x00, 0x0A, 0x00, 0x14];
        await Assert.That(ImageHeaderReader.TryReadDimensions(sof1, out _)).IsTrue();

        byte[] sof3ShortSeg = [0xFF, 0xD8, 0xFF, 0xC3, 0x00, 0x04, 0x00, 0x00];
        await Assert.That(ImageHeaderReader.TryReadDimensions(sof3ShortSeg, out _)).IsFalse();

        byte[] nonMarkerByte = [0xFF, 0xD8, 0x12, 0x34, 0x56, 0x78];
        await Assert.That(ImageHeaderReader.TryReadDimensions(nonMarkerByte, out _)).IsFalse();

        byte[] trailingPadding = [0xFF, 0xD8, 0xFF, 0xFF, 0xFF, 0xFF];
        await Assert.That(ImageHeaderReader.TryReadDimensions(trailingPadding, out _)).IsFalse();

        byte[] truncatedAfterMarker = [0xFF, 0xD8, 0xFF, 0xE0, 0x00];
        await Assert.That(ImageHeaderReader.TryReadDimensions(truncatedAfterMarker, out _)).IsFalse();

        byte[] badSegLengthZero = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x01];
        await Assert.That(ImageHeaderReader.TryReadDimensions(badSegLengthZero, out _)).IsFalse();

        byte[] badSegLengthOverflow = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x20, 0x00];
        await Assert.That(ImageHeaderReader.TryReadDimensions(badSegLengthOverflow, out _)).IsFalse();

        byte[] eofAfterValidSegment = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x02];
        await Assert.That(ImageHeaderReader.TryReadDimensions(eofAfterValidSegment, out _)).IsFalse();

        byte[] wrongSecondByte = [0xFF, 0x00, 0x00, 0x00];
        await Assert.That(ImageHeaderReader.TryReadDimensions(wrongSecondByte, out _)).IsFalse();
    }

    /// <summary>Verifies AVIF / HEIF ISO BMFF <c>ftyp</c> + <c>ispe</c> box dimension extraction.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryReadDimensions_AvifIsoBmffIspe_ParsesAndRejectsTruncatedBoxes()
    {
        byte[] avif = [0, 0, 0, 0x10, .. FtypAvif, 0, 0, 0, 0, .. "ispe\0\0\0\0"u8, 0, 0, 0x02, 0x80, 0, 0, 0x01, 0x68];
        await Assert.That(ImageHeaderReader.TryReadDimensions(avif, out var dims)).IsTrue();
        await Assert.That(dims).IsEqualTo(ExpectedSample);

        byte[] missingIspe = [0, 0, 0, 0x10, .. FtypAvif, 0, 0, 0, 0];
        await Assert.That(ImageHeaderReader.TryReadDimensions(missingIspe, out _)).IsFalse();

        byte[] truncatedIspe = [0, 0, 0, 0x10, .. FtypAvif, 0, 0, 0, 0, .. "ispe\0\0"u8];
        await Assert.That(ImageHeaderReader.TryReadDimensions(truncatedIspe, out _)).IsFalse();
    }

    /// <summary>Verifies SVG width/height attributes, <c>px</c> suffixes, percentage fallback to <c>viewBox</c>, BOM handling, and invalid SVG cases.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryReadDimensions_SvgAttributesAndViewBox_CoversAllSvgBranches()
    {
        byte[] bomSvg = [0xEF, 0xBB, 0xBF, .. "<svg stroke-width=\"2\" width=\"640px\" height='360'></svg>"u8];
        await Assert.That(ImageHeaderReader.TryReadDimensions(bomSvg, out var pxDims)).IsTrue();
        await Assert.That(pxDims).IsEqualTo(ExpectedSample);

        var viewBoxSvg = "<svg width=\"100%\" height=\"100%\" viewBox=\"0, 0\t640 360\"></svg>"u8;
        await Assert.That(ImageHeaderReader.TryReadDimensions(viewBoxSvg, out var vbDims)).IsTrue();
        await Assert.That(vbDims).IsEqualTo(ExpectedSample);

        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg unclosed"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg width></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg width2=\"10\" height=\"10\"></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg width=10 height=10></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg width=\"unclosed></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg width=\"-5\" height=\"10\"></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg width=\"0.1\" height=\"10\"></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg width=\"10\" height=\"bad\"></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg width=\"\" viewBox=\"0 0 bad 10\"></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg viewBox=\"0 0 10\"></svg>"u8, out _)).IsFalse();
        await Assert.That(ImageHeaderReader.TryReadDimensions("<svg viewBox=\"0 0 10 20 extra\"></svg>"u8, out var extraVb)).IsTrue();
        await Assert.That(extraVb).IsEqualTo(ExpectedSmall);

        var hugePrefix = Encoding.UTF8.GetBytes($"{new string(' ', OversizedSvgPrefix)}<svg width=\"10\" height=\"10\"></svg>");
        await Assert.That(ImageHeaderReader.TryReadDimensions(hugePrefix, out _)).IsFalse();
    }
}
