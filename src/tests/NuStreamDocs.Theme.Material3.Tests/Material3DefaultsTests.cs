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

    /// <summary>Pages stamp a <c>build-date</c> meta with an ISO 8601 timestamp.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildDateMetaIsStamped()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts.WithSiteName("Site"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        var iso = ExtractBuildDate(html);

        // ISO 8601 round-trip ("o") starts with YYYY-MM-DDTHH:MM:SS.
        await Assert.That(iso.Length).IsGreaterThanOrEqualTo(19);
        await Assert.That(iso[4]).IsEqualTo('-');
        await Assert.That(iso[7]).IsEqualTo('-');
        await Assert.That(iso[10]).IsEqualTo('T');
        await Assert.That(iso[13]).IsEqualTo(':');
        await Assert.That(iso[16]).IsEqualTo(':');
    }

    /// <summary>Copyright values containing the literal token <c>{year}</c> have it expanded to the current four-digit year.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CopyrightYearTokenExpands()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts
                .WithSiteName("Site")
                .WithCopyright("(c) {year} Acme — and friends"u8))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        var year = DateTimeOffset.UtcNow.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Assert.That(html).Contains($"(c) {year} Acme — and friends");
        await Assert.That(html).DoesNotContain("{year}");
    }

    /// <summary>Copyrights without the <c>{year}</c> token render verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CopyrightWithoutTokenRendersVerbatim()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts
                .WithSiteName("Site")
                .WithCopyright("(c) Acme Corp"u8))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        await Assert.That(html).Contains("(c) Acme Corp");
    }

    /// <summary>The repo-source card pins its own foreground colour so dark headers don't render the GitHub icon white-on-white inside the light card.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SourceCardScopesItsOwnForegroundColour()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts.WithSiteName("Site"))
            .BuildAsync();

        var css = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "assets", "stylesheets", "material3.css"));
        await Assert.That(css).Contains(".md-source__icon");

        // The .md-source rule pins its colour explicitly to on-surface so a header
        // foreground override (e.g. white text on a coloured header) doesn't bleed
        // into the card surface.
        await Assert.That(css).Contains(".md-source {");
        await Assert.That(css).Contains("color: var(--md-sys-color-on-surface)");
    }

    /// <summary>The bundled stylesheet keeps the content article pinned to the centre grid track even when sidebars are hidden, so leaf pages aren't squeezed into the sidebar-width column.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ContentArticlePinnedToCentreTrack()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme(static opts => opts.WithSiteName("Site"))
            .BuildAsync();

        var css = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "assets", "stylesheets", "material3.css"));
        await Assert.That(css).Contains(".md-main__inner > .md-content");
        await Assert.That(css).Contains("grid-column: 2");
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

    /// <summary>Pulls the <c>build-date</c> meta value out of <paramref name="html"/>.</summary>
    /// <param name="html">Rendered page HTML.</param>
    /// <returns>The captured timestamp, or empty when the meta isn't present.</returns>
    private static string ExtractBuildDate(string html)
    {
        const string Marker = "<meta name=\"build-date\" content=\"";
        var idx = html.IndexOf(Marker, StringComparison.Ordinal);
        if (idx < 0)
        {
            return string.Empty;
        }

        var rest = html.AsSpan(idx + Marker.Length);
        var end = rest.IndexOf('"');
        return end > 0 ? new(rest[..end]) : string.Empty;
    }
}
