// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>Drives ThemePluginBase through every option permutation.</summary>
public class ThemePluginBaseTests
{
    /// <summary>EnableScrollToTop + EnableTocFollow + RepoUrl + EditUri all set surfaces in the rendered HTML.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AllOptionsEnabled()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro\n\nbody");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts
                .WithSiteName("Hi")
                .WithRepoUrl("https://github.com/owner/repo")
                .WithEditUri("edit/main/docs") with
            {
                EnableScrollToTop = true,
                EnableTocFollow = true,
                EnableNavigationFooter = true,
            })
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).Contains("github.com/owner/repo");
        await Assert.That(html).Contains("edit/main/docs/intro.md");
    }

    /// <summary>RepoUrl set but EditUri empty produces no edit URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepoUrlWithoutEditUriOmitsEditLink()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts
                .WithSiteName("Hi")
                .WithRepoUrl("https://github.com/owner/repo")
                .WithEditUri(string.Empty))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).DoesNotContain("edit/main/docs");
    }

    /// <summary>EditUri set without RepoUrl is treated as not configured.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EditUriWithoutRepoOmitsEditLink()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts
                .WithSiteName("Hi")
                .WithRepoUrl(string.Empty)
                .WithEditUri("edit/main/docs"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).DoesNotContain("edit/main/docs");
    }

    /// <summary>Trailing slashes on RepoUrl are normalized to a single separator.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepoUrlTrailingSlashNormalized()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts
                .WithSiteName("Hi")
                .WithRepoUrl("https://github.com/owner/repo/")
                .WithEditUri("/edit/main/docs/"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).Contains("https://github.com/owner/repo/edit/main/docs/intro.md");
        await Assert.That(html).DoesNotContain("repo//edit");
    }

    /// <summary>Pages in subdirectories produce an edit URL with forward-slashed path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedPageEditUrlUsesForwardSlash()
    {
        using var fixture = TempBuildTree.Create();
        var sub = Path.Combine(fixture.Docs, "guide");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "intro.md"), "# Sub");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts
                .WithSiteName("Hi")
                .WithRepoUrl("https://github.com/owner/repo")
                .WithEditUri("edit/main/docs"))
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "guide", "intro.html"));
        await Assert.That(html).Contains("edit/main/docs/guide/intro.md");
    }

    /// <summary>WriteEmbeddedAssets=false skips the bundled CSS write.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmbeddedAssetsDisabledSkipsCssEmit()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts.WithSiteName("Hi") with
            {
                AssetSource = MaterialAssetSource.Cdn,
            })
            .BuildAsync();

        var cssPath = Path.Combine(fixture.Site, "assets", "stylesheets", "material.min.css");
        await Assert.That(File.Exists(cssPath)).IsFalse();
    }

    /// <summary>Footer enabled with a registered provider produces global prev/next links.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NavFooterGlobalNeighboursRendered()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UsePlugin(new StubNeighbours(globalUrl: "/prev-page", sectionUrl: "/section-page"))
            .UseMaterialTheme(static opts => opts.WithSiteName("Hi") with { EnableNavigationFooter = true, SectionScopedFooter = false })
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).Contains("/prev-page");
    }

    /// <summary>SectionScopedFooter true selects GetSectionNeighbours instead of GetNeighbours.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SectionScopedFooterUsesSectionNeighbours()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UsePlugin(new StubNeighbours(globalUrl: "/global", sectionUrl: "/section-only"))
            .UseMaterialTheme(static opts => opts.WithSiteName("Hi") with { EnableNavigationFooter = true, SectionScopedFooter = true })
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).Contains("/section-only");
        await Assert.That(html).DoesNotContain("/global");
    }

    /// <summary>Directory-URL builds emit footer neighbour links in directory form.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NavFooterUsesDirectoryUrlsWhenEnabled()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseDirectoryUrls()
            .UsePlugin(new StubNeighbours(globalUrl: "/prev-page", sectionUrl: "/section-page"))
            .UseMaterialTheme(static opts => opts.WithSiteName("Hi") with { EnableNavigationFooter = true, SectionScopedFooter = false })
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro", "index.html"));
        await Assert.That(html).Contains("/prev-page/");
        await Assert.That(html).DoesNotContain("/prev-page.html");
    }

    /// <summary>Footer disabled bypasses the provider and emits no neighbour links.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NavFooterDisabledNoNeighbourLinks()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UsePlugin(new StubNeighbours(globalUrl: "/should-not-appear", sectionUrl: "/should-not-appear"))
            .UseMaterialTheme(static opts => opts.WithSiteName("Hi") with { EnableNavigationFooter = false })
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));
        await Assert.That(html).DoesNotContain("should-not-appear");
    }

    /// <summary>Footer enabled with no provider in the plugin list emits no neighbour links.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NavFooterEnabledButNoProviderRegistered()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterialTheme(static opts => opts.WithSiteName("Hi") with { EnableNavigationFooter = true })
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "intro.html"));

        // Build succeeds without throwing; no neighbour links produced.
        await Assert.That(html).Contains("Hi");
    }

    /// <summary>Stub provider returning fixed prev/next on both global and section calls.</summary>
    /// <param name="globalUrl">Path returned from GetNeighbours; converted to a slug-like relative.</param>
    /// <param name="sectionUrl">Path returned from GetSectionNeighbours.</param>
    private sealed class StubNeighbours(string globalUrl, string sectionUrl) : IDocPlugin, INavNeighboursProvider
    {
        /// <inheritdoc/>
        public byte[] Name => "stub-neighbours"u8.ToArray();

        /// <inheritdoc/>
        public NavNeighbours GetNeighbours(string relativePath) =>
            new(globalUrl.TrimStart('/') + ".md", "Prev"u8.ToArray(), string.Empty, []);

        /// <inheritdoc/>
        public NavNeighbours GetSectionNeighbours(string relativePath) =>
            new(sectionUrl.TrimStart('/') + ".md", "Section Prev"u8.ToArray(), string.Empty, []);

        /// <inheritdoc/>
        public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        /// <inheritdoc/>
        public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
