// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav.Tests;

/// <summary>Parameterized inputs for NavTreeBuilder covering every combination of sort, prune, indexes, and hide-empty toggles.</summary>
public class NavTreeBuilderParameterizedTests
{
    /// <summary>Each <see cref="NavSortBy"/> mode produces a tree with the expected page count.</summary>
    /// <param name="sort">Sort mode.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(NavSortBy.FileName)]
    [Arguments(NavSortBy.Title)]
    [Arguments(NavSortBy.None)]
    public async Task SortModes(NavSortBy sort)
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("a.md", "# A\n");
        await temp.WriteAsync("b.md", "# B\n");
        var options = NavOptions.Default with { SortBy = sort };
        var root = NavTreeBuilder.Build(temp.Root, options);
        await Assert.That(root.Children.Length).IsEqualTo(2);
    }

    /// <summary>Indexes toggle controls whether index.md becomes a section index.</summary>
    /// <param name="indexes">Whether to detect index pages.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task IndexesToggle(bool indexes)
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("guide/index.md", "# Guide\n");
        await temp.WriteAsync("guide/intro.md", "# Intro\n");
        var options = NavOptions.Default with { Indexes = indexes };
        var root = NavTreeBuilder.Build(temp.Root, options);
        await Assert.That(root.Children.Length).IsGreaterThan(0);
    }

    /// <summary>HideEmptySections toggle accepts both values without error.</summary>
    /// <param name="hide">Whether to hide empty sections.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task HideEmptySectionsToggle(bool hide)
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("a.md", "# A\n");
        Directory.CreateDirectory(Path.Combine(temp.Root, "empty"));
        var options = NavOptions.Default with { HideEmptySections = hide };
        var root = NavTreeBuilder.Build(temp.Root, options);
        await Assert.That(root).IsNotNull();
    }

    /// <summary>Include/exclude patterns filter the input set.</summary>
    /// <param name="patterns">Glob patterns.</param>
    /// <param name="expectedChildren">Expected top-level child count.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(new[] { "*.md" }, 2)]
    [Arguments(new[] { "a.md" }, 1)]
    [Arguments(new string[0], 2)]
    public async Task IncludePatterns(string[] patterns, int expectedChildren)
    {
        ArgumentNullException.ThrowIfNull(patterns);
        using ScratchTree temp = new();
        await temp.WriteAsync("a.md", "# A\n");
        await temp.WriteAsync("b.md", "# B\n");
        var includeGlobs = new Common.GlobPattern[patterns.Length];
        for (var i = 0; i < patterns.Length; i++)
        {
            includeGlobs[i] = patterns[i];
        }

        var options = NavOptions.Default with { Includes = includeGlobs };
        var root = NavTreeBuilder.Build(temp.Root, options);
        await Assert.That(root.Children.Length).IsEqualTo(expectedChildren);
    }

    /// <summary>Auto-discovered pages use the front-matter title instead of the file stem.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FrontmatterTitleWinsForAutoDiscovery()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("license.md", "---\ntitle: Licenses & Credits\n---\n# License\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default);
        await Assert.That(System.Text.Encoding.UTF8.GetString(root.Children[0].Title)).IsEqualTo("Licenses & Credits");
    }

    /// <summary>Auto-discovered pages fall back to the first H1 before the file stem.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task H1TitleWinsWhenFrontmatterTitleMissing()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("getting-started.md", "# Getting Started with ReactiveUI\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default);
        await Assert.That(System.Text.Encoding.UTF8.GetString(root.Children[0].Title)).IsEqualTo("Getting Started with ReactiveUI");
    }

    /// <summary>Section titles humanize the directory token when no override is present.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SectionTitlesHumanizeDirectoryNames()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("getting-started/index.md", "# Getting Started with ReactiveUI\n");
        await temp.WriteAsync("getting-started/install.md", "# Install\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default);
        await Assert.That(System.Text.Encoding.UTF8.GetString(root.Children[0].Title)).IsEqualTo("Getting Started");
    }

    /// <summary>Directory-URL mode stores section and page hrefs in served form.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlsFlowIntoBuiltNodes()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("guide/index.md", "# Guide\n");
        await temp.WriteAsync("guide/intro.md", "# Intro\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default, useDirectoryUrls: true);
        await Assert.That(System.Text.Encoding.UTF8.GetString(root.Children[0].IndexUrlBytes)).IsEqualTo("guide/");
        await Assert.That(System.Text.Encoding.UTF8.GetString(root.Children[0].Children[0].RelativeUrlBytes)).IsEqualTo("guide/intro/");
    }

    /// <summary>
    /// Section titles prefer the section's own <c>index.md</c> frontmatter <c>title:</c> when
    /// present so authored names beat the humanized directory fallback. Without this, deep API
    /// trees show "Akavache.Settings" (directory name) instead of the readable title set by the
    /// page author.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SectionTitlePrefersIndexFrontmatter()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync(
            "Akavache.Settings/index.md",
            "---\ntitle: Akavache Settings\n---\n# overview\n");
        await temp.WriteAsync("Akavache.Settings/page.md", "# Page\n");

        var options = NavOptions.Default with { Indexes = true };
        var root = NavTreeBuilder.Build(temp.Root, options);

        var sectionTitle = System.Text.Encoding.UTF8.GetString(root.Children[0].Title);
        await Assert.That(sectionTitle).IsEqualTo("Akavache Settings");
    }

    /// <summary>Section title falls back to the humanized directory name when the index page has no frontmatter <c>title</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SectionTitleFallsBackToDirectoryName()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("getting-started/index.md", "# Welcome\n");
        await temp.WriteAsync("getting-started/page.md", "# Page\n");

        var options = NavOptions.Default with { Indexes = true };
        var root = NavTreeBuilder.Build(temp.Root, options);

        var sectionTitle = System.Text.Encoding.UTF8.GetString(root.Children[0].Title);
        await Assert.That(sectionTitle).IsEqualTo("Getting Started");
    }

    /// <summary>The <c>.pages</c> override <c>title:</c> wins over both the index frontmatter title and the directory name.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PagesOverrideTitleWinsOverIndexFrontmatter()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync(
            "guide/index.md",
            "---\ntitle: Authored Title\n---\n# Welcome\n");
        await temp.WriteAsync("guide/page.md", "# Page\n");
        await temp.WriteAsync("guide/.pages", "title: Override Title\n");

        var options = NavOptions.Default with { Indexes = true };
        var root = NavTreeBuilder.Build(temp.Root, options);

        var sectionTitle = System.Text.Encoding.UTF8.GetString(root.Children[0].Title);
        await Assert.That(sectionTitle).IsEqualTo("Override Title");
    }

    /// <summary>Pages with explicit <c>Order:</c> sort by that integer first; alpha breaks ties + handles unordered pages.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExplicitOrderSortsBeforeAlpha()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("alpha-late.md", "---\nOrder: 5\n---\n# Late\n");
        await temp.WriteAsync("bravo-early.md", "---\nOrder: 1\n---\n# Early\n");
        await temp.WriteAsync("charlie-no-order.md", "# Charlie\n");
        await temp.WriteAsync("delta-no-order.md", "# Delta\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default);

        var pathOrder = string.Join(",", root.Children.Select(static c => Path.GetFileNameWithoutExtension(c.RelativePath.Value)));
        await Assert.That(pathOrder).IsEqualTo("bravo-early,alpha-late,charlie-no-order,delta-no-order");
    }

    /// <summary>Sections inherit <c>Order:</c> from their <c>index.md</c> frontmatter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SectionOrderHonoursIndexFrontmatter()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("alpha/index.md", "---\nOrder: 5\n---\n# Alpha\n");
        await temp.WriteAsync("alpha/p.md", "# P\n");
        await temp.WriteAsync("bravo/index.md", "---\nOrder: 1\n---\n# Bravo\n");
        await temp.WriteAsync("bravo/p.md", "# P\n");
        await temp.WriteAsync("charlie/index.md", "# Charlie\n");
        await temp.WriteAsync("charlie/p.md", "# P\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default with { Indexes = true });

        var sectionOrder = string.Join(",", root.Children.Where(static c => c.IsSection).Select(static c => Path.GetFileName(c.RelativePath.Value)));
        await Assert.That(sectionOrder).IsEqualTo("bravo,alpha,charlie");
    }

    /// <summary>Explicit <c>Order:</c> sorts pages and sections together when any child has one — an Order: 1 section comes before an Order: 6 leaf page.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExplicitOrderInterleavesPagesAndSections()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("Slack.md", "---\nOrder: 4\n---\n# Slack\n");
        await temp.WriteAsync("Book.md", "---\nOrder: 6\n---\n# Book\n");
        await temp.WriteAsync("license.md", "# License\n");
        await temp.WriteAsync("docs/index.md", "---\nOrder: 1\n---\n# Docs\n");
        await temp.WriteAsync("docs/page.md", "# P\n");
        await temp.WriteAsync("api/index.md", "---\nOrder: 2\n---\n# API\n");
        await temp.WriteAsync("api/page.md", "# P\n");
        await temp.WriteAsync("contribute/index.md", "---\nOrder: 3\n---\n# C\n");
        await temp.WriteAsync("contribute/page.md", "# P\n");
        await temp.WriteAsync("vs/index.md", "---\nOrder: 5\n---\n# V\n");
        await temp.WriteAsync("vs/page.md", "# P\n");
        await temp.WriteAsync("Announcements/post.md", "# Post\n");
        await temp.WriteAsync("articles/post.md", "# Post\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default with { Indexes = true });

        var ordered = string.Join(",", root.Children.Select(static c => c.IsSection ? Path.GetFileName(c.RelativePath.Value) : Path.GetFileNameWithoutExtension(c.RelativePath.Value)));
        await Assert.That(ordered).IsEqualTo("docs,api,contribute,Slack,vs,Book,Announcements,articles,license");
    }

    /// <summary>Without any explicit <c>Order:</c>, the historical pages-first-sections-last layout is preserved (no behaviour change for unannotated trees).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnannotatedTreesKeepPagesFirstThenSections()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("alpha.md", "# A\n");
        await temp.WriteAsync("zulu.md", "# Z\n");
        await temp.WriteAsync("guide/index.md", "# G\n");
        await temp.WriteAsync("guide/p.md", "# P\n");
        await temp.WriteAsync("api/index.md", "# A\n");
        await temp.WriteAsync("api/p.md", "# P\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default with { Indexes = true });

        var ordered = string.Join(",", root.Children.Select(static c => c.IsSection ? Path.GetFileName(c.RelativePath.Value) : Path.GetFileNameWithoutExtension(c.RelativePath.Value)));

        // Pages alpha-sorted first, then sections alpha-sorted — the existing default.
        await Assert.That(ordered).IsEqualTo("alpha,zulu,api,guide");
    }

    /// <summary>Same explicit <c>Order:</c> on multiple pages falls back to alpha.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DuplicateOrderTieBreaksAlphabetically()
    {
        using ScratchTree temp = new();
        await temp.WriteAsync("zulu.md", "---\nOrder: 1\n---\n# Z\n");
        await temp.WriteAsync("alpha.md", "---\nOrder: 1\n---\n# A\n");
        await temp.WriteAsync("mike.md", "---\nOrder: 1\n---\n# M\n");

        var root = NavTreeBuilder.Build(temp.Root, NavOptions.Default);

        var pathOrder = string.Join(",", root.Children.Select(c => Path.GetFileNameWithoutExtension(c.RelativePath.Value)));
        await Assert.That(pathOrder).IsEqualTo("alpha,mike,zulu");
    }

    /// <summary>Disposable scratch tree.</summary>
    private sealed class ScratchTree : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchTree"/> class.</summary>
        public ScratchTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-nav-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch root.</summary>
        public string Root { get; }

        /// <summary>Writes <paramref name="content"/> to <paramref name="relativePath"/> under <see cref="Root"/>.</summary>
        /// <param name="relativePath">Path relative to the scratch root.</param>
        /// <param name="content">UTF-8 file contents.</param>
        /// <returns>Async I/O task.</returns>
        public Task WriteAsync(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return File.WriteAllTextAsync(path, content);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
