// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="Css2StylesheetParser"/>.</summary>
public class Css2StylesheetParserTests
{
    /// <summary>A captured two-block Google <c>css2</c> response.</summary>
    private const string GoogleCss = """
                                     /* latin */
                                     @font-face {
                                       font-family: 'Source Sans 3';
                                       font-style: normal;
                                       font-weight: 400;
                                       font-display: swap;
                                       src: url(https://fonts.gstatic.com/s/sourcesans3/v18/abc.woff2) format('woff2');
                                       unicode-range: U+0000-00FF, U+0131, U+0152-0153;
                                     }
                                     /* latin-ext */
                                     @font-face {
                                       font-family: 'Source Sans 3';
                                       font-style: italic;
                                       font-weight: 700;
                                       font-display: swap;
                                       src: url(https://fonts.gstatic.com/s/sourcesans3/v18/xyz.woff2) format('woff2');
                                       unicode-range: U+0100-024F;
                                     }
                                     """;

    /// <summary>Both <c>@font-face</c> blocks are parsed with their weight, style, range, and url.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ParsesGoogleCss2Response()
    {
        var faces = Css2StylesheetParser.Parse(Encoding.UTF8.GetBytes(GoogleCss));
        await Assert.That(faces.Length).IsEqualTo(2);

        await Assert.That(faces[0].Weight).IsEqualTo(400);
        await Assert.That(faces[0].Style).IsEqualTo(FontStyle.Normal);
        await Assert.That(((string)faces[0].Woff2Url).EndsWith("abc.woff2", StringComparison.Ordinal)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(faces[0].UnicodeRange)).Contains("U+0000-00FF");
        await Assert.That(Encoding.UTF8.GetString(faces[0].SubsetName)).IsEqualTo("latin");

        await Assert.That(faces[1].Weight).IsEqualTo(700);
        await Assert.That(faces[1].Style).IsEqualTo(FontStyle.Italic);
        await Assert.That(((string)faces[1].Woff2Url).EndsWith("xyz.woff2", StringComparison.Ordinal)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(faces[1].SubsetName)).IsEqualTo("latin-ext");
    }

    /// <summary>A relative <c>url(./files/...)</c> (Fontsource style) is returned verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ParsesRelativeUrl()
    {
        const string Css = """
                           @font-face {
                             font-family: 'JetBrains Mono';
                             font-style: normal;
                             font-weight: 400;
                             src: url(./files/jetbrains-mono-latin-400-normal.woff2) format('woff2');
                             unicode-range: U+0000-00FF;
                           }
                           """;
        var faces = Css2StylesheetParser.Parse(Encoding.UTF8.GetBytes(Css));
        await Assert.That(faces.Length).IsEqualTo(1);
        await Assert.That((string)faces[0].Woff2Url).IsEqualTo("./files/jetbrains-mono-latin-400-normal.woff2");
        await Assert.That(faces[0].SubsetName.Length).IsEqualTo(0);
    }

    /// <summary>An empty / unrelated stylesheet yields no faces.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyYieldsNothing()
    {
        await Assert.That(Css2StylesheetParser.Parse("body { color: red; }"u8).Length).IsEqualTo(0);
        await Assert.That(Css2StylesheetParser.Parse([]).Length).IsEqualTo(0);
    }
}
