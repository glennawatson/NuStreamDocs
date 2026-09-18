// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="FontCssWriter"/>.</summary>
public class FontCssWriterTests
{
    /// <summary>Expected normal weight in the fixture.</summary>
    private const int NormalWeight = 400;

    /// <summary>Expected bold weight in the fixture.</summary>
    private const int BoldWeight = 700;

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

    /// <summary>The stylesheet has an <c>@font-face</c> per resource, the fallback face, and the theme-variable wiring.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WritesFacesFallbackAndRoot()
    {
        FontCssWriter.ResourceCss[] resources =
        [
            new(NormalWeight, FontStyle.Normal, [.. "U+0000-00FF"u8], [.. "assets/fonts/aaaa.woff2"u8]),
            new(BoldWeight, FontStyle.Italic, [], [.. "assets/fonts/bbbb.woff2"u8])
        ];
        FontCssWriter.FaceCss[] faces =
        [
            new(
                [.. "source-sans-3"u8],
                [.. "Source Sans 3"u8],
                FontDisplay.Swap,
                GenericFontFamily.SansSerif,
                [[.. "--md-text-font"u8]],
                new FontMetrics(UnitsPerEm, Ascender, Descender, 0, XHeight, CapHeight),
                resources)
        ];

        ArrayBufferWriter<byte> sink = new();
        FontCssWriter.Write(faces, sink);
        var css = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(css)
            .Contains(
                "@font-face{font-family:\"Source Sans 3\";font-style:normal;font-weight:400;unicode-range:U+0000-00FF;");
        await Assert.That(css).Contains("font-display:swap;src:url(\"/assets/fonts/aaaa.woff2\") format(\"woff2\");}");
        await Assert.That(css)
            .Contains("font-style:italic;font-weight:700;font-display:swap;src:url(\"/assets/fonts/bbbb.woff2\")");
        await Assert.That(css).Contains("font-family:\"Source Sans 3 fallback\"");
        await Assert.That(css)
            .Contains("--nstd-font-source-sans-3:\"Source Sans 3\",\"Source Sans 3 fallback\",sans-serif;");
        await Assert.That(css).Contains("--md-text-font:var(--nstd-font-source-sans-3);");
    }

    /// <summary>A face without metrics emits no fallback <c>@font-face</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoMetricsOmitsFallbackFace()
    {
        FontCssWriter.FaceCss[] faces =
        [
            new(
                [.. "x"u8],
                [.. "X"u8],
                FontDisplay.Swap,
                GenericFontFamily.SansSerif,
                [],
                null,
                [new(NormalWeight, FontStyle.Normal, [], [.. "assets/fonts/cccc.woff2"u8])])
        ];
        ArrayBufferWriter<byte> sink = new();
        FontCssWriter.Write(faces, sink);
        var css = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(css).DoesNotContain("@font-face{font-family:\"X fallback\"");
        await Assert.That(css).Contains("--nstd-font-x:\"X\",\"X fallback\",sans-serif;");
    }
}
