// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="FontsourceProvider"/> (offline, fixture-fed).</summary>
public class FontsourceProviderTests
{
    /// <summary>The jsDelivr stylesheet URL is built from the package id, subset, weight, and style.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildsStylesheetUrl()
    {
        var normal = (string)FontsourceProvider.BuildStylesheetUrl("jetbrains-mono", "latin", 400, FontStyle.Normal);
        var italic = (string)FontsourceProvider.BuildStylesheetUrl("jetbrains-mono", "latin", 700, FontStyle.Italic);
        await Assert.That(normal)
            .IsEqualTo("https://cdn.jsdelivr.net/npm/@fontsource/jetbrains-mono@latest/latin-400.css");
        await Assert.That(italic)
            .IsEqualTo("https://cdn.jsdelivr.net/npm/@fontsource/jetbrains-mono@latest/latin-700-italic.css");
    }

    /// <summary>Resolving fetches the stylesheet then the relative woff2 (resolved against the stylesheet URL) from the cache.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolvesRelativeWoff2()
    {
        using TempDir dir = new();
        var cache = new FontDownloadCache(dir.Root, true);
        var face = FontsOptions.Default.AddFace(new(
            [.. "jetbrains-mono"u8],
            [.. "JetBrains Mono"u8],
            FontProviderKind.Fontsource,
            [400],
            [FontStyle.Normal],
            [[.. "latin"u8]],
            FontDisplay.Swap,
            true,
            GenericFontFamily.Monospace,
            [],
            [])).Faces[0];

        const string Css = """
                           @font-face {
                             font-family: 'JetBrains Mono';
                             font-style: normal;
                             font-weight: 400;
                             src: url(./files/jetbrains-mono-latin-400-normal.woff2) format('woff2');
                             unicode-range: U+0000-00FF;
                           }
                           """;
        var cssUrl = FontsourceProvider.BuildStylesheetUrl("jetbrains-mono", "latin", 400, FontStyle.Normal);
        await File.WriteAllBytesAsync(cache.CacheFilePath(cssUrl), Encoding.UTF8.GetBytes(Css));
        const string Woff2Url =
            "https://cdn.jsdelivr.net/npm/@fontsource/jetbrains-mono@latest/files/jetbrains-mono-latin-400-normal.woff2";
        byte[] woff2Bytes = [9, 8, 7];
        await File.WriteAllBytesAsync(cache.CacheFilePath(Woff2Url), woff2Bytes);

        var resources = await FontsourceProvider.Instance.ResolveAsync(
            face,
            [[.. "latin"u8]],
            cache,
            default,
            null,
            CancellationToken.None);
        await Assert.That(resources.Length).IsEqualTo(1);
        await Assert.That(resources[0].Weight).IsEqualTo(400);
        await Assert.That(resources[0].Woff2Bytes.SequenceEqual(woff2Bytes)).IsTrue();
        await Assert.That((string)resources[0].SourceUrl).IsEqualTo(Woff2Url);
    }
}
