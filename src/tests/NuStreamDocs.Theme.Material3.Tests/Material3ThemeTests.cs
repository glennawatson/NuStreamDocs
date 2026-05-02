// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>End-to-end tests for the Material 3 theme assembly.</summary>
public class Material3ThemeTests
{
    /// <summary>Loading the theme should compile every shipped template and asset.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LoadCompilesAllTemplatesAndAssets()
    {
        var theme = Material3Theme.Load();

        await Assert.That(theme.Page.InstructionCount).IsGreaterThan(0);
        await Assert.That(theme.Partials.GetAlternateLookup<ReadOnlySpan<byte>>().ContainsKey("header"u8)).IsTrue();
        await Assert.That(theme.Partials.GetAlternateLookup<ReadOnlySpan<byte>>().ContainsKey("sidebar"u8)).IsTrue();
        await Assert.That(theme.StaticAssets.ContainsKey("assets/stylesheets/material3.css")).IsTrue();
        await Assert.That(theme.StaticAssets.ContainsKey("assets/javascripts/material3.js")).IsTrue();
    }

    /// <summary>Embedded mode should write the bundle and wrap pages in the MD3 shell.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmbeddedModeWritesAssetsAndWrapsPages()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro\n\nbody");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts with { SiteName = "MD3 Site" })
            .BuildAsync();

        var cssPath = Path.Combine(fixture.Site, "assets", "stylesheets", "material3.css");
        await Assert.That(File.Exists(cssPath)).IsTrue();

        var pagePath = Path.Combine(fixture.Site, "intro.html");
        var html = await File.ReadAllTextAsync(pagePath);
        await Assert.That(html).Contains("<!doctype html>");
        await Assert.That(html).Contains("MD3 Site");
        await Assert.That(html).Contains("data-md-color-scheme=\"default\"");
        await Assert.That(html).Contains("href=\"assets/stylesheets/material3.css\"");
        await Assert.That(html).Contains("<h1>");
    }

    /// <summary>CDN mode should skip the asset write and point at the configured CDN URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CdnModeSkipsAssetsAndPointsAtCdn()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts with
            {
                AssetSource = Material3AssetSource.Cdn,
                CdnRoot = "https://example.test/md3",
            })
            .BuildAsync();

        var cssPath = Path.Combine(fixture.Site, "assets", "stylesheets", "material3.css");
        await Assert.That(File.Exists(cssPath)).IsFalse();

        var pagePath = Path.Combine(fixture.Site, "page.html");
        var html = await File.ReadAllTextAsync(pagePath);
        await Assert.That(html).Contains("https://example.test/md3/stylesheets/material3.css");
    }
}
