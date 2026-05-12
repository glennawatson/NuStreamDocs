// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="FallbackFaceBuilder"/>.</summary>
public class FallbackFaceBuilderTests
{
    /// <summary>The override fractions match the size-adjust-then-scale formula against the Arial reference.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ComputeOverridesMatchesFormula()
    {
        // Web font: em=1000, ascent=950, descent=-250, gap=0, xHeight=500. Arial ref: em=2048, xHeight=1062.
        var web = new FontMetrics(1000, 950, -250, 0, 500, 700);
        var o = FallbackFaceBuilder.ComputeOverrides(web, GenericFontFamily.SansSerif);
        const double expectedSizeAdjust = 500.0 / 1000.0 / (1062.0 / 2048.0);
        await Assert.That(o.SizeAdjust).IsEqualTo(expectedSizeAdjust).Within(1e-9);
        await Assert.That(o.AscentOverride).IsEqualTo(950.0 / 1000.0 / expectedSizeAdjust).Within(1e-9);
        await Assert.That(o.DescentOverride).IsEqualTo(250.0 / 1000.0 / expectedSizeAdjust).Within(1e-9);
        await Assert.That(o.LineGapOverride).IsEqualTo(0.0).Within(1e-9);
    }

    /// <summary>A font without OS/2 x-height falls back to size-adjust 1.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoXHeightYieldsSizeAdjustOne()
    {
        var web = new FontMetrics(1000, 800, -200, 0, 0, 0);
        await Assert.That(FallbackFaceBuilder.ComputeOverrides(web, GenericFontFamily.SansSerif).SizeAdjust).IsEqualTo(1.0);
    }

    /// <summary>The written rule names the fallback family, has a local() src, and the four metric descriptors.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteEmitsFallbackFace()
    {
        ArrayBufferWriter<byte> sink = new();
        FallbackFaceBuilder.Write("Source Sans 3"u8, new FontMetrics(1000, 950, -250, 0, 500, 700), GenericFontFamily.SansSerif, sink);
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
        FallbackFaceBuilder.Write("JetBrains Mono"u8, new FontMetrics(1000, 1020, -300, 0, 550, 730), GenericFontFamily.Monospace, sink);
        var css = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(css).Contains("local(\"Courier New\")");
        await Assert.That(ReferenceFontMetrics.KeywordFor(GenericFontFamily.Monospace).SequenceEqual("monospace"u8)).IsTrue();
    }
}
