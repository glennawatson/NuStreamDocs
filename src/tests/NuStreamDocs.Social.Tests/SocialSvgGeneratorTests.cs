// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Social.Tests;

/// <summary>Unit tests for <see cref="SocialSvgGenerator"/>.</summary>
public sealed class SocialSvgGeneratorTests
{
    /// <summary>Byte budget for two-line space-wrapping test.</summary>
    private const int SpaceWrapLimit = 12;

    /// <summary>Byte budget for hard UTF-8 boundary cut test.</summary>
    private const int HardCutLimit = 8;

    /// <summary>Length of the synthetic continuation-byte buffer.</summary>
    private const int ContinuationRunLength = 20;

    /// <summary>UTF-8 continuation byte value.</summary>
    private const byte ContinuationByte = 0x80;

    /// <summary>Verifies full SVG card output with escaped entities, wrapped title/description, and stripped trailing slash on the footer URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RenderCard_FullInputs_EmitsValidEscapedSvg()
    {
        var svgBytes = SocialSvgGenerator.RenderCard(
            "Streaming <UTF-8> & Fast \"Docs\" Builder For Modern .NET Libraries"u8,
            "Zero-allocation parsers, deterministic social preview cards, OpenGraph metadata, and Schema.org structured data out of the box for static sites."u8,
            "NuStreamDocs"u8,
            "https://docs.example.com/"u8,
            SocialOptions.Default);

        var svg = Encoding.UTF8.GetString(svgBytes);
        await Assert.That(svg).Contains("width=\"1200\" height=\"630\"");
        await Assert.That(svg).Contains("Streaming &lt;UTF-8&gt; &amp; Fast");
        await Assert.That(svg).Contains("NuStreamDocs");
        await Assert.That(svg).Contains("https://docs.example.com</text>");
        await Assert.That(svg).Contains("...");
    }

    /// <summary>Verifies minimal SVG card rendering when optional fields and color arrays are empty and URL has no trailing slash.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RenderCard_EmptyOptionalFieldsAndColorFallbacks_UsesDefaultColors()
    {
        var options = new SocialOptions { BackgroundColor = [], AccentColor = [] };
        var svgBytes = SocialSvgGenerator.RenderCard(
            "Short"u8,
            default,
            default,
            "https://docs.example.com"u8,
            options);

        var svg = Encoding.UTF8.GetString(svgBytes);
        await Assert.That(svg).Contains("fill=\"#0f172a\"");
        await Assert.That(svg).Contains("fill=\"#2563eb\"");
        await Assert.That(svg).Contains(">Short</text>");
        await Assert.That(svg).Contains(">https://docs.example.com</text>");
    }

    /// <summary>Verifies <see cref="SocialSvgGenerator.SplitTwoLines"/> handles two-line fits and unbroken multi-byte words cleanly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SplitTwoLines_CoversTwoLineFitAndHardUtf8BoundaryCuts()
    {
        SocialSvgGenerator.SplitTwoLines("Hello world again"u8, SpaceWrapLimit, out var first, out var second, out var truncated);
        var firstText = Encoding.UTF8.GetString(first);
        var secondText = Encoding.UTF8.GetString(second);

        await Assert.That(firstText).IsEqualTo("Hello world");
        await Assert.That(secondText).IsEqualTo("again");
        await Assert.That(truncated).IsFalse();

        SocialSvgGenerator.SplitTwoLines("abcdefg🚀hijklmnop"u8, HardCutLimit, out var emojiFirst, out var emojiSecond, out var emojiTruncated);
        var emojiFirstText = Encoding.UTF8.GetString(emojiFirst);
        var emojiSecondHasContent = !emojiSecond.IsEmpty;

        await Assert.That(emojiFirstText).IsEqualTo("abcdefg");
        await Assert.That(emojiSecondHasContent).IsTrue();
        await Assert.That(emojiTruncated).IsTrue();

        var rawContinuations = new byte[ContinuationRunLength];
        Array.Fill(rawContinuations, ContinuationByte);
        SocialSvgGenerator.SplitTwoLines(rawContinuations, HardCutLimit, out var contFirst, out _, out var contTruncated);
        var contFirstBytes = contFirst.ToArray();

        await Assert.That(contFirstBytes.Length).IsEqualTo(0);
        await Assert.That(contTruncated).IsTrue();
    }
}
