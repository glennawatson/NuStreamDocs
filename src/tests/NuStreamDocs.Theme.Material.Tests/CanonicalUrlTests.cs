// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>End-to-end coverage for <c>SiteUrl</c> driving canonical / og:url emission.</summary>
public class CanonicalUrlTests
{
    /// <summary>Pretty URLs map <c>foo/bar.md</c> to a directory-slug canonical URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PrettyUrlsEmitDirectoryCanonical()
    {
        using var fixture = TempBuildTree.Create();
        var sub = Path.Combine(fixture.Docs, "guide");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "intro.md"), "# Sub");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseDirectoryUrls()
            .UseMaterialTheme(static opts => opts.WithSiteName("Site").WithSiteUrl("https://example.test/"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "guide", "intro", "index.html"));
        await Assert.That(html).Contains("rel=\"canonical\" href=\"https://example.test/guide/intro/\"");
        await Assert.That(html).Contains("property=\"og:url\" content=\"https://example.test/guide/intro/\"");
        await Assert.That(html).Contains("property=\"og:site_name\" content=\"Site\"");
    }

    /// <summary>Flat URLs map <c>foo/bar.md</c> to <c>foo/bar.html</c> in the canonical URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FlatUrlsEmitHtmlCanonical()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts.WithSiteUrl("https://example.test"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).Contains("rel=\"canonical\" href=\"https://example.test/intro.html\"");
    }

    /// <summary>Empty <c>SiteUrl</c> suppresses the canonical and og:url tags entirely.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptySiteUrlOmitsCanonical()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts.WithSiteName("Hi"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).DoesNotContain("rel=\"canonical\"");
        await Assert.That(html).DoesNotContain("property=\"og:url\"");
    }

    /// <summary>An <c>index.md</c> at the root maps to the bare site URL with a trailing slash.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RootIndexMapsToBareSiteUrl()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "index.md"), "# Home");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseDirectoryUrls()
            .UseMaterialTheme(static opts => opts.WithSiteUrl("https://example.test"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "index.html"));
        await Assert.That(html).Contains("rel=\"canonical\" href=\"https://example.test/\"");
    }
}
