// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Nav.Tests;

/// <summary>Behavior tests for nav rendering, including <c>navigation.prune</c>.</summary>
public class NavRendererTests
{
    /// <summary>The full renderer emits every page even when the active page is deep in the tree.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FullRendererEmitsEveryNode()
    {
        var intro = new NavNode("Intro", "guide/intro.md", isSection: false, []);
        var post = new NavNode("Post", "blog/post.md", isSection: false, []);
        var guide = new NavNode("Guide", "guide", isSection: true, [intro], indexPath: "guide/index.md");
        var blog = new NavNode("Blog", "blog", isSection: true, [post], indexPath: "blog/index.md");
        var root = new NavNode(string.Empty, string.Empty, isSection: true, [guide, blog]);
        root.AttachParents();

        var writer = new ArrayBufferWriter<byte>();
        NavRenderer.RenderFull(root, intro, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html.Contains("/guide/intro.html", StringComparison.Ordinal)).IsTrue();
        await Assert.That(html.Contains("/blog/post.html", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>The pruned renderer drops sections outside the active branch.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrunedRendererCollapsesSectionsOutsideActiveBranch()
    {
        var html = await RenderAsync(prune: true, currentPage: "guide/intro.html");

        await Assert.That(html.Contains("guide/intro.html", StringComparison.Ordinal)).IsTrue();

        // Blog section's child page should be hidden when pruning while the
        // active page is in guide/. The "Blog" label still appears as a
        // collapsed section header.
        await Assert.That(html.Contains("blog/post.html", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>The active page is flagged with <c>md-nav__link--active</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ActiveLeafGetsActiveClass()
    {
        var html = await RenderAsync(prune: false, currentPage: "guide/intro.html");
        await Assert.That(html.Contains("md-nav__link--active", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>The scoped sidebar shows only the active top-level section instead of repeating every top-level tab.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidebarScopesToActiveTopLevelSection()
    {
        var home = new NavNode("Home", "index.md", isSection: false, []);
        var intro = new NavNode("Intro", "guide/intro.md", isSection: false, []);
        var post = new NavNode("Post", "blog/post.md", isSection: false, []);
        var guide = new NavNode("Guide", "guide", isSection: true, [intro]);
        var blog = new NavNode("Blog", "blog", isSection: true, [post]);
        var root = new NavNode(string.Empty, string.Empty, isSection: true, [home, guide, blog]);
        root.AttachParents();

        var writer = new ArrayBufferWriter<byte>();
        NavRenderer.RenderSidebarFull(root, intro, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("/index.html");
        await Assert.That(html).Contains("/guide/");
        await Assert.That(html).Contains("/guide/intro.html");
        await Assert.That(html).DoesNotContain("/blog/post.html");
    }

    /// <summary>Section tabs without a promoted index page still link to the section root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabsLinkToSectionRootWhenSectionHasNoIndexPage()
    {
        var home = new NavNode("Home", "index.md", isSection: false, []);
        var apiIndex = new NavNode("API home", "api/index.md", isSection: false, []);
        var api = new NavNode("API", "api", isSection: true, [apiIndex]);
        var root = new NavNode(string.Empty, string.Empty, isSection: true, [home, api]);
        root.AttachParents();

        var writer = new ArrayBufferWriter<byte>();
        NavRenderer.RenderTabs(root, apiIndex, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("href=\"/api/\"");
        await Assert.That(html).Contains("md-tabs__item--active");
    }

    /// <summary>Leaf links render as root-relative hrefs so docs pages do not resolve them against the current directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidebarLeafLinksAreRootRelative()
    {
        var intro = new NavNode("Intro", "guide/intro.md", isSection: false, []);
        var guide = new NavNode("Guide", "guide", isSection: true, [intro], indexPath: "guide/index.md");
        var root = new NavNode(string.Empty, string.Empty, isSection: true, [guide]);
        root.AttachParents();

        var writer = new ArrayBufferWriter<byte>();
        NavRenderer.RenderSidebarFull(root, intro, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("href=\"/guide/index.html\"");
        await Assert.That(html).Contains("href=\"/guide/intro.html\"");
    }

    /// <summary>Directory-URL section index pages scope the sidebar to their active top-level section.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryUrlSectionIndexScopesSidebar()
    {
        using var fixture = TempDocsTree.Create();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "guide"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "blog"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# Index");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide", "index.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide", "intro.md"), "# Intro");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "blog", "index.md"), "# Blog");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "blog", "post.md"), "# Post");

        var options = NavOptions.Default with { Tabs = true, UseDirectoryUrls = true };
        var plugin = new NavPlugin(options);
        var configureContext = new PluginConfigureContext(fixture.Root, fixture.Output, [plugin]) { UseDirectoryUrls = true };
        await plugin.OnConfigureAsync(configureContext, CancellationToken.None);

        var page = new ArrayBufferWriter<byte>();
        page.Write("<nav>"u8);
        page.Write(NavPlugin.NavMarker);
        page.Write("</nav>"u8);

        var renderContext = new PluginRenderContext("guide/index.md", ReadOnlyMemory<byte>.Empty, page);
        await plugin.OnRenderPageAsync(renderContext, CancellationToken.None);

        var html = Encoding.UTF8.GetString(page.WrittenSpan);
        await Assert.That(html).Contains("Home");
        await Assert.That(html).Contains("href=\"/guide/\"");
        await Assert.That(html).Contains("href=\"/guide/intro/\"");
        await Assert.That(html).DoesNotContain("href=\"/blog/post/\"");
    }

    /// <summary>Sidebar sections without an index page link to the first real descendant page.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidebarSectionWithoutIndexUsesFirstDescendantHref()
    {
        var blogs = new NavNode("Blogs", "docs/resources/blogs.md", isSection: false, [], useDirectoryUrls: true);
        var videos = new NavNode("Videos", "docs/resources/videos.md", isSection: false, [], useDirectoryUrls: true);
        var resources = new NavNode("Resources", "docs/resources", isSection: true, [blogs, videos], useDirectoryUrls: true);
        var root = new NavNode(string.Empty, string.Empty, isSection: true, [resources], useDirectoryUrls: true);
        root.AttachParents();

        var writer = new ArrayBufferWriter<byte>();
        NavRenderer.RenderSidebarPruned(root, resources, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("href=\"/docs/resources/blogs/\"");
        await Assert.That(html).DoesNotContain("href=\"/docs/resources/\"");
    }

    /// <summary>BuildUrlIndex maps every leaf URL to its node.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlIndexMapsEveryLeaf()
    {
        var root = BuildSampleTree();
        var index = NavRenderer.BuildUrlIndex(root);

        await Assert.That(index.ContainsKeyByUtf8("guide/intro.html"u8)).IsTrue();
        await Assert.That(index.ContainsKeyByUtf8("blog/post.html"u8)).IsTrue();
        await Assert.That(index.TryGetValueByUtf8("guide/intro.html"u8, out var introNode) && Encoding.UTF8.GetString(introNode.Title) == "Intro").IsTrue();
    }

    /// <summary>BuildUrlIndex includes section index URLs (when present).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlIndexIncludesSectionIndex()
    {
        var leaf = new NavNode("Intro", "guide/intro.md", isSection: false, []);

        // Sections expose IndexUrl via the 5-arg ctor's indexPath argument.
        var section = new NavNode("Guide", relativePath: string.Empty, isSection: true, [leaf], indexPath: "guide/index.md");
        var root = new NavNode(string.Empty, string.Empty, isSection: true, [section]);

        var index = NavRenderer.BuildUrlIndex(root);

        await Assert.That(index.ContainsKeyByUtf8("guide/intro.html"u8)).IsTrue();
        await Assert.That(index.ContainsKeyByUtf8("guide/index.html"u8)).IsTrue();
        await Assert.That(index.TryGetValueByUtf8("guide/index.html"u8, out var guideNode) && Encoding.UTF8.GetString(guideNode.Title) == "Guide").IsTrue();
    }

    /// <summary>BuildUrlIndex returns an empty dictionary for an empty tree.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlIndexEmptyTree()
    {
        var root = new NavNode(string.Empty, string.Empty, isSection: true, []);
        var index = NavRenderer.BuildUrlIndex(root);
        await Assert.That(index.Count).IsEqualTo(0);
    }

    /// <summary>BuildUrlIndex throws when root is null.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlIndexNullRootThrows() =>
        await Assert.That(() => NavRenderer.BuildUrlIndex(null!)).Throws<ArgumentNullException>();

    /// <summary>Builds the standard 2-section fixture and runs the plugin's per-page render path.</summary>
    /// <param name="prune">Prune mode flag.</param>
    /// <param name="currentPage">URL of the page being rendered.</param>
    /// <returns>The rendered HTML for the page.</returns>
    private static async Task<string> RenderAsync(bool prune, string currentPage)
    {
        using var fixture = TempDocsTree.Create();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "guide"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "blog"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# Index");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide", "intro.md"), "# Intro");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "blog", "post.md"), "# Post");

        var options = NavOptions.Default with { Prune = prune };
        var plugin = new NavPlugin(options);
        var configureContext = new PluginConfigureContext(fixture.Root, fixture.Output, [plugin]);
        await plugin.OnConfigureAsync(configureContext, CancellationToken.None);

        var page = new ArrayBufferWriter<byte>();

        // Surrogate "themed" payload — just the marker and trailing tag.
        page.Write("<nav>"u8);
        page.Write(NavPlugin.NavMarker);
        page.Write("</nav>"u8);

        var sourcePath = currentPage.Replace(".html", ".md", StringComparison.Ordinal);
        var renderContext = new PluginRenderContext(sourcePath, ReadOnlyMemory<byte>.Empty, page);
        await plugin.OnRenderPageAsync(renderContext, CancellationToken.None);

        return Encoding.UTF8.GetString(page.WrittenSpan);
    }

    /// <summary>Builds a small two-section tree used by the index-positive tests.</summary>
    /// <returns>Tree root.</returns>
    private static NavNode BuildSampleTree()
    {
        var intro = new NavNode("Intro", "guide/intro.md", isSection: false, []);
        var post = new NavNode("Post", "blog/post.md", isSection: false, []);
        var guide = new NavNode("Guide", string.Empty, isSection: true, [intro]);
        var blog = new NavNode("Blog", string.Empty, isSection: true, [post]);
        return new(string.Empty, string.Empty, isSection: true, [guide, blog]);
    }
}
