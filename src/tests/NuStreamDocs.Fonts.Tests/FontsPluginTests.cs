// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>End-to-end coverage for <see cref="FontsPlugin"/> using the local provider (offline).</summary>
public class FontsPluginTests
{
    /// <summary>With no declared faces, the plugin contributes nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoFacesContributesNothing()
    {
        var plugin = new FontsPlugin();
        using TempDir dir = new();
        await plugin.ConfigureAsync(new(dir.Root, dir.Root, [], new()), CancellationToken.None);
        await Assert.That(plugin.StaticAssets.Length).IsEqualTo(0);
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
        await Assert.That(plugin.Name.SequenceEqual("fonts"u8)).IsTrue();
    }

    /// <summary>A local face produces a hashed woff2 asset, a fonts.css that defines the face, the fallback face, and the theme variable, plus a preload + stylesheet link in the head.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LocalFaceEmitsAssetsCssAndHeadLinks()
    {
        using TempDir dir = new();
        Directory.CreateDirectory(Path.Combine(dir.Root, "fonts"));
        var woff2 = StubFont.BuildWoff2(unitsPerEm: 2048, ascender: 1900, descender: -500, lineGap: 0, xHeight: 1082, capHeight: 1462);
        await File.WriteAllBytesAsync(Path.Combine(dir.Root, "fonts", "MyFont-Regular.woff2"), woff2);

        var face = new FontFace(
            [.. "myfont"u8],
            [.. "MyFont"u8],
            FontProviderKind.Local,
            [400],
            [FontStyle.Normal],
            [],
            FontDisplay.Swap,
            true,
            GenericFontFamily.SansSerif,
            ["fonts/MyFont-*.woff2"],
            [[.. "--md-text-font"u8]]);
        var plugin = new FontsPlugin(FontsOptions.Default.AddFace(face));
        await plugin.ConfigureAsync(new(dir.Root, Path.Combine(dir.Root, "site"), [], new()), CancellationToken.None);

        var assets = plugin.StaticAssets;
        await Assert.That(assets.Length).IsEqualTo(2);
        byte[]? cssBytes = null;
        var hasWoff2 = false;
        for (var i = 0; i < assets.Length; i++)
        {
            if (assets[i].Path.Value == "assets/fonts/fonts.css")
            {
                cssBytes = assets[i].Bytes;
            }
            else if (assets[i].Path.Value.StartsWith("assets/fonts/", StringComparison.Ordinal) && assets[i].Path.Value.EndsWith(".woff2", StringComparison.Ordinal))
            {
                hasWoff2 = true;
                await Assert.That(assets[i].Bytes.SequenceEqual(woff2)).IsTrue();
            }
        }

        await Assert.That(hasWoff2).IsTrue();
        await Assert.That(cssBytes).IsNotNull();
        var css = Encoding.UTF8.GetString(cssBytes!);
        await Assert.That(css).Contains("@font-face{font-family:\"MyFont\";");
        await Assert.That(css).Contains("@font-face{font-family:\"MyFont fallback\"");
        await Assert.That(css).Contains("--md-text-font:var(--nstd-font-myfont);");

        ArrayBufferWriter<byte> head = new();
        plugin.WriteHeadExtra(head);
        var headHtml = Encoding.UTF8.GetString(head.WrittenSpan);
        await Assert.That(headHtml).Contains("rel=\"preload\"");
        await Assert.That(headHtml).Contains("as=\"font\"");
        await Assert.That(headHtml).Contains("<link rel=\"stylesheet\" href=\"/assets/fonts/fonts.css\">");
    }
}
