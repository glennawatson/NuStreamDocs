// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>End-to-end tests for the friendly defaults the theme ships out of the box.</summary>
public class Material3DefaultsTests
{
    /// <summary>When no favicon is configured and the docs tree is empty, the build still emits the bundled default favicon.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultFaviconEmittedWhenNothingConfigured()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts.WithSiteName("Site"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        await Assert.That(html).Contains("rel=\"icon\"");
        await Assert.That(html).Contains("/assets/images/favicon.svg");

        var faviconAsset = Path.Combine(fixture.Site, "assets", "images", "favicon.svg");
        await Assert.That(File.Exists(faviconAsset)).IsTrue();
    }

    /// <summary>A docs-tree favicon under the mkdocs-material convention is auto-discovered with no config.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DocsTreeFaviconIsAutoDiscovered()
    {
        using var fixture = TempBuildTree.Create();
        var faviconDir = Path.Combine(fixture.Docs, "images", "favicons");
        Directory.CreateDirectory(faviconDir);
        await File.WriteAllBytesAsync(Path.Combine(faviconDir, "favicon.ico"), [0x00, 0x00, 0x01, 0x00]);
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts.WithSiteName("Site"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        await Assert.That(html).Contains("/images/favicons/favicon.ico");
        await Assert.That(html).DoesNotContain("/assets/images/favicon.svg");
    }

    /// <summary>An explicit <c>WithFavicon</c> wins over both auto-discovery and the embedded default.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExplicitWithFaviconWinsOverDiscoveryAndDefault()
    {
        using var fixture = TempBuildTree.Create();
        var faviconDir = Path.Combine(fixture.Docs, "images", "favicons");
        Directory.CreateDirectory(faviconDir);
        await File.WriteAllBytesAsync(Path.Combine(faviconDir, "favicon.ico"), [0x00, 0x00, 0x01, 0x00]);
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts
                .WithSiteName("Site")
                .WithFavicon("/custom/path.svg"u8))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        await Assert.That(html).Contains("/custom/path.svg");
        await Assert.That(html).DoesNotContain("/images/favicons/favicon.ico");
    }

    /// <summary>Builds emit a synthetic <c>404.html</c> at site root when no <c>404.md</c> was authored.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultNotFoundPageIsEmitted()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts.WithSiteName("My Site"))
            .BuildAsync();

        var notFound = Path.Combine(fixture.Site, "404.html");
        await Assert.That(File.Exists(notFound)).IsTrue();

        var html = await File.ReadAllTextAsync(notFound);
        await Assert.That(html).Contains("Page not found");
        await Assert.That(html).Contains("My Site");
        await Assert.That(html).Contains("rel=\"stylesheet\"");
    }

    /// <summary>An authored <c>404.md</c> is rendered through the normal pipeline and lands at <c>/404.html</c> regardless of directory URLs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task Authored404MdLandsAtRootRegardlessOfDirectoryUrls()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "404.md"), "# Custom Not Found\n\nMy custom message.");
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseDirectoryUrls()
            .UseMaterial3Theme(static opts => opts.WithSiteName("Site"))
            .BuildAsync();

        var rootNotFound = Path.Combine(fixture.Site, "404.html");
        var directoryStyleNotFound = Path.Combine(fixture.Site, "404", "index.html");

        await Assert.That(File.Exists(rootNotFound)).IsTrue();
        await Assert.That(File.Exists(directoryStyleNotFound)).IsFalse();

        var html = await File.ReadAllTextAsync(rootNotFound);
        await Assert.That(html).Contains("Custom Not Found");
        await Assert.That(html).Contains("My custom message");
    }

    /// <summary>The synthetic 404 emitter steps aside when a real <c>404.html</c> is already on disk.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SyntheticNotFoundDoesNotOverwriteAuthored404()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "404.md"), "# Authored Not Found");
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts.WithSiteName("Site"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "404.html"));
        await Assert.That(html).Contains("Authored Not Found");
        await Assert.That(html).DoesNotContain("Page not found");
    }
}
