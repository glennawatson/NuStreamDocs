// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="FallbackFaceBuilder"/>.</summary>
public class FallbackFaceBuilderTests
{
    /// <summary>Expected units per em in the fixture.</summary>
    private const int UnitsPerEm = 1000;

    /// <summary>Expected ascender in the fixture.</summary>
    private const int Ascender = 950;

    /// <summary>Expected descender in the fixture.</summary>
    private const int Descender = -250;

    /// <summary>Height of the fixture's lowercase x.</summary>
    private const int XHeight = 500;

    /// <summary>Expected cap height in the fixture.</summary>
    private const int CapHeight = 700;

    /// <summary>Expected tolerance in the fixture.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>Ascender used in the expected scaling formula.</summary>
    private const double ExpectedAscender = 950.0;

    /// <summary>Units per em used in the expected scaling formula.</summary>
    private const double ExpectedUnitsPerEm = 1000.0;

    /// <summary>Descent magnitude used in the expected scaling formula.</summary>
    private const double ExpectedDescent = 250.0;

    /// <summary>Expected short ascender in the fixture.</summary>
    private const int ShortAscender = 800;

    /// <summary>Expected short descender in the fixture.</summary>
    private const int ShortDescender = -200;

    /// <summary>Expected mono ascender in the fixture.</summary>
    private const int MonoAscender = 1020;

    /// <summary>Expected mono descender in the fixture.</summary>
    private const int MonoDescender = -300;

    /// <summary>Height of the monospace fixture's lowercase x.</summary>
    private const int MonoXHeight = 550;

    /// <summary>Expected mono cap height in the fixture.</summary>
    private const int MonoCapHeight = 730;

    /// <summary>The override fractions match the size-adjust-then-scale formula against the Arial reference.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ComputeOverridesMatchesFormula()
    {
        // Web font: em=1000, ascent=950, descent=-250, gap=0, xHeight=500. Arial ref: em=2048, xHeight=1062.
        var web = new FontMetrics(UnitsPerEm, Ascender, Descender, 0, XHeight, CapHeight);
        var o = FallbackFaceBuilder.ComputeOverrides(web, GenericFontFamily.SansSerif);
        const double ExpectedSizeAdjust = 500.0 / 1000.0 / (1062.0 / 2048.0);
        await Assert.That(o.SizeAdjust).IsEqualTo(ExpectedSizeAdjust).Within(Tolerance);
        await Assert.That(o.AscentOverride).IsEqualTo(ExpectedAscender / ExpectedUnitsPerEm / ExpectedSizeAdjust).Within(Tolerance);
        await Assert.That(o.DescentOverride).IsEqualTo(ExpectedDescent / ExpectedUnitsPerEm / ExpectedSizeAdjust).Within(Tolerance);
        await Assert.That(o.LineGapOverride).IsEqualTo(0.0).Within(Tolerance);
    }

    /// <summary>A font without OS/2 x-height falls back to size-adjust 1.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoXHeightYieldsSizeAdjustOne()
    {
        var web = new FontMetrics(UnitsPerEm, ShortAscender, ShortDescender, 0, 0, 0);
        await Assert.That(FallbackFaceBuilder.ComputeOverrides(web, GenericFontFamily.SansSerif).SizeAdjust)
            .IsEqualTo(1.0);
    }

    /// <summary>The written rule names the fallback family, has a local() src, and the four metric descriptors.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteEmitsFallbackFace()
    {
        ArrayBufferWriter<byte> sink = new();
        FallbackFaceBuilder.Write(
            "Source Sans 3"u8,
            new(UnitsPerEm, Ascender, Descender, 0, XHeight, CapHeight),
            GenericFontFamily.SansSerif,
            sink);
        var css = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(css).Contains("font-family:\"Source Sans 3 fallback\"");
        await Assert.That(css).Contains("local(\"Arial\")");
        await Assert.That(css).Contains("ascent-override:");
        await Assert.That(css).Contains("descent-override:");
        await Assert.That(css).Contains("line-gap-override:");
        await Assert.That(css).Contains("size-adjust:");
    }

    /// <summary>The monospace variant uses the Courier New reference and the <c>monospace</c> keyword.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MonospaceUsesCourierReference()
    {
        ArrayBufferWriter<byte> sink = new();
        FallbackFaceBuilder.Write(
            "JetBrains Mono"u8,
            new(UnitsPerEm, MonoAscender, MonoDescender, 0, MonoXHeight, MonoCapHeight),
            GenericFontFamily.Monospace,
            sink);
        var css = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(css).Contains("local(\"Courier New\")");
        await Assert.That(ReferenceFontMetrics.KeywordFor(GenericFontFamily.Monospace).SequenceEqual("monospace"u8))
            .IsTrue();
    }
}
