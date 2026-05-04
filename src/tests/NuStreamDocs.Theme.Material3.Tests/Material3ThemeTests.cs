// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Search;

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
        await Assert.That(theme.StaticAssets.ContainsKey("assets/images/favicon.svg")).IsTrue();
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
            .UseMaterial3Theme(static opts => opts.WithSiteName("MD3 Site"))
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

    /// <summary>Search-enabled builds render the Material 3 search surface and richer repository badge scaffolding.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SearchAndRepoBadgeRenderInHeader()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseSearch()
            .UseMaterial3Theme(static opts => opts
                .WithSiteName("MD3 Site")
                .WithRepoUrl("https://github.com/owner/repo"))
            .BuildAsync();

        var pagePath = Path.Combine(fixture.Site, "page.html");
        var html = await File.ReadAllTextAsync(pagePath);
        await Assert.That(html).Contains("data-md-component=\"search\"");
        await Assert.That(html).Contains("data-md-component=\"search-results\"");
        await Assert.That(html).Contains("name=\"nustreamdocs:search-index\"");
        await Assert.That(html).Contains("https://github.com/owner/repo");
        await Assert.That(html).Contains("data-md-component=\"source-facts\"");
        await Assert.That(html).Contains("<input");
        await Assert.That(html).Contains("data-md-component=\"search-query\"");
        await Assert.That(html).Contains("data-md-component=\"search-close\"");
    }

    /// <summary>
    /// With <c>DocBuilder.UseDirectoryUrls()</c> on, a non-index page like
    /// <c>api/Akavache.Settings/Akavache/Settings/AkavacheBuilderAsyncExtensions.md</c> serves at
    /// <c>/api/Akavache.Settings/Akavache/Settings/AkavacheBuilderAsyncExtensions/</c>, so the
    /// document is one directory deeper than the source path implies. The page's relative
    /// asset references must therefore use one extra <c>../</c> to reach the site root, otherwise
    /// the page loads with no stylesheets, scripts, or icons.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlNonIndexPagesUseExtraDoubleDotForAssets()
    {
        using var fixture = TempBuildTree.Create();
        var deepDir = Path.Combine(fixture.Docs, "api", "Akavache.Settings", "Akavache", "Settings");
        Directory.CreateDirectory(deepDir);
        await File.WriteAllTextAsync(Path.Combine(deepDir, "AkavacheBuilderAsyncExtensions.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseDirectoryUrls()
            .UseMaterial3Theme(static opts => opts.WithSiteName("MD3 Site"))
            .BuildAsync();

        var pagePath = Path.Combine(fixture.Site, "api", "Akavache.Settings", "Akavache", "Settings", "AkavacheBuilderAsyncExtensions", "index.html");
        var html = await File.ReadAllTextAsync(pagePath);

        // Source path has 4 slashes; with directory URLs the served page sits at depth 5,
        // so the CSS href must climb 5 levels — `../../../../../assets/...`. Anchor on the
        // `href="` prefix so the assertion can't accept a shorter (under-climbing) prefix as a
        // substring of the correct one.
        await Assert.That(html).Contains("href=\"../../../../../assets/stylesheets/material3.css\"");
        await Assert.That(html).DoesNotContain("href=\"../../../../assets/stylesheets/material3.css\"");
    }

    /// <summary>
    /// Without directory URLs, the served path matches the source path's depth — no extra
    /// <c>../</c> hop. Same deeply nested layout, asserting the directory-URL fix doesn't
    /// regress the non-directory-URL case.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonDirectoryUrlPagesKeepDepthMatchingSourcePath()
    {
        using var fixture = TempBuildTree.Create();
        var deepDir = Path.Combine(fixture.Docs, "api", "Akavache.Settings", "Akavache", "Settings");
        Directory.CreateDirectory(deepDir);
        await File.WriteAllTextAsync(Path.Combine(deepDir, "AkavacheBuilderAsyncExtensions.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts.WithSiteName("MD3 Site"))
            .BuildAsync();

        var pagePath = Path.Combine(fixture.Site, "api", "Akavache.Settings", "Akavache", "Settings", "AkavacheBuilderAsyncExtensions.html");
        var html = await File.ReadAllTextAsync(pagePath);

        // Source path has 4 slashes; without directory URLs the served page sits at depth 4,
        // so the CSS href climbs 4 levels — `../../../../assets/...`.
        await Assert.That(html).Contains("href=\"../../../../assets/stylesheets/material3.css\"");
        await Assert.That(html).DoesNotContain("href=\"../../../../../assets/stylesheets/material3.css\"");
    }

    /// <summary>
    /// Index pages (<c>foo/index.md</c>) under directory URLs serve at the parent directory's URL
    /// (<c>/foo/</c>), so depth is unchanged — only non-index pages get the extra <c>../</c> hop.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlIndexPagesDoNotAddExtraDoubleDot()
    {
        using var fixture = TempBuildTree.Create();
        var apiDir = Path.Combine(fixture.Docs, "api");
        Directory.CreateDirectory(apiDir);
        await File.WriteAllTextAsync(Path.Combine(apiDir, "index.md"), "# API");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseDirectoryUrls()
            .UseMaterial3Theme(static opts => opts.WithSiteName("MD3 Site"))
            .BuildAsync();

        var pagePath = Path.Combine(fixture.Site, "api", "index.html");
        var html = await File.ReadAllTextAsync(pagePath);

        // Source path has 1 slash and is an index page → served at /api/ → depth 1.
        await Assert.That(html).Contains("href=\"../assets/stylesheets/material3.css\"");
        await Assert.That(html).DoesNotContain("href=\"../../assets/stylesheets/material3.css\"");
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
            .UseMaterial3Theme(static opts => opts.WithCdnRoot("https://example.test/md3") with
            {
                AssetSource = Material3AssetSource.Cdn,
            })
            .BuildAsync();

        var cssPath = Path.Combine(fixture.Site, "assets", "stylesheets", "material3.css");
        await Assert.That(File.Exists(cssPath)).IsFalse();

        var pagePath = Path.Combine(fixture.Site, "page.html");
        var html = await File.ReadAllTextAsync(pagePath);
        await Assert.That(html).Contains("https://example.test/md3/stylesheets/material3.css");
    }
}
