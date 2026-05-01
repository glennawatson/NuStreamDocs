// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Templating;

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>End-to-end tests for the Material theme assembly.</summary>
public class MaterialThemeTests
{
    /// <summary>Loading the theme should compile every shipped template and asset.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoadCompilesAllTemplatesAndAssets()
    {
        var theme = MaterialTheme.Load();

        await Assert.That(theme.Page.InstructionCount).IsGreaterThan(0);
        await Assert.That(theme.Partials.ContainsKey("header")).IsTrue();
        await Assert.That(theme.Partials.ContainsKey("sidebar")).IsTrue();
        await Assert.That(theme.StaticAssets.ContainsKey("assets/stylesheets/material.min.css")).IsTrue();
    }

    /// <summary>Rendering the page template against a small data scope should produce a Material shell.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PageTemplateRendersMaterialShell()
    {
        var theme = MaterialTheme.Load();
        var data = new TemplateData(
            new(StringComparer.Ordinal)
            {
                ["language"] = (byte[])[.. "en"u8],
                ["site_name"] = (byte[])[.. "Test Site"u8],
                ["site_root"] = (byte[])[.. "/"u8],
                ["page_title"] = (byte[])[.. "Hi"u8],
                ["body"] = (byte[])[.. "<h1>Hello</h1>"u8],
                ["asset_root"] = (byte[])[.. "assets"u8],
                ["copyright"] = (byte[])[.. ""u8],
            },
            sections: null);

        var writer = new ArrayBufferWriter<byte>();
        theme.Page.Render(data, theme.Partials, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);

        await Assert.That(html).Contains("<!doctype html>");
        await Assert.That(html).Contains("Test Site");
        await Assert.That(html).Contains("<h1>Hello</h1>");
        await Assert.That(html).Contains("assets/stylesheets/material.min.css");
    }

    /// <summary>Embedded mode should write the bundle and wrap pages in the Material shell.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmbeddedModeWritesAssetsAndWrapsPages()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro\n\nbody");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts with { SiteName = "Hi" })
            .BuildAsync();

        var cssPath = Path.Combine(fixture.Site, "assets", "stylesheets", "material.min.css");
        await Assert.That(File.Exists(cssPath)).IsTrue();

        var pagePath = Path.Combine(fixture.Site, "intro.html");
        var html = await File.ReadAllTextAsync(pagePath);
        await Assert.That(html).Contains("<!doctype html>");
        await Assert.That(html).Contains("Hi");
        await Assert.That(html).Contains("<h1>");
        await Assert.That(html).Contains("href=\"assets/stylesheets/material.min.css\"");
    }

    /// <summary>CDN mode should skip the asset write and point the page template at the CDN URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CdnModeSkipsAssetsAndPointsAtCdn()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts with
            {
                AssetSource = MaterialAssetSource.Cdn,
                CdnRoot = "https://example.test/material",
            })
            .BuildAsync();

        var cssPath = Path.Combine(fixture.Site, "assets", "stylesheets", "material.min.css");
        await Assert.That(File.Exists(cssPath)).IsFalse();

        var pagePath = Path.Combine(fixture.Site, "page.html");
        var html = await File.ReadAllTextAsync(pagePath);
        await Assert.That(html).Contains("https://example.test/material/stylesheets/material.min.css");
    }
}
