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
        NavNode intro = new("Intro", "guide/intro.md", isSection: false, []);
        NavNode post = new("Post", "blog/post.md", isSection: false, []);
        NavNode guide = new("Guide", "guide", isSection: true, [intro], indexPath: "guide/index.md");
        NavNode blog = new("Blog", "blog", isSection: true, [post], indexPath: "blog/index.md");
        NavNode root = new(string.Empty, string.Empty, isSection: true, [guide, blog]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, intro);
        NavRenderer.RenderFull(tree, activeIdx, writer);

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
        NavNode home = new("Home", "index.md", isSection: false, []);
        NavNode intro = new("Intro", "guide/intro.md", isSection: false, []);
        NavNode post = new("Post", "blog/post.md", isSection: false, []);
        NavNode guide = new("Guide", "guide", isSection: true, [intro]);
        NavNode blog = new("Blog", "blog", isSection: true, [post]);
        NavNode root = new(string.Empty, string.Empty, isSection: true, [home, guide, blog]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, intro);
        NavRenderer.RenderSidebarFull(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("/index.html");
        await Assert.That(html).Contains("/guide/intro.html");
        await Assert.That(html).DoesNotContain(">Guide<");
        await Assert.That(html).DoesNotContain("/blog/post.html");
    }

    /// <summary>Section tabs without a promoted index page still link to the section root.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabsLinkToSectionRootWhenSectionHasNoIndexPage()
    {
        NavNode home = new("Home", "index.md", isSection: false, []);
        NavNode apiIndex = new("API home", "api/index.md", isSection: false, []);
        NavNode api = new("API", "api", isSection: true, [apiIndex]);
        NavNode root = new(string.Empty, string.Empty, isSection: true, [home, api]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, apiIndex);
        NavRenderer.RenderTabs(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("href=\"/api/\"");
        await Assert.That(html).Contains("md-tabs__item--active");
    }

    /// <summary>Contextual sidebar leaf links remain root-relative after the active section wrapper is flattened away.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidebarLeafLinksAreRootRelative()
    {
        NavNode intro = new("Intro", "guide/intro.md", isSection: false, []);
        NavNode guide = new("Guide", "guide", isSection: true, [intro], indexPath: "guide/index.md");
        NavNode root = new(string.Empty, string.Empty, isSection: true, [guide]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, intro);
        NavRenderer.RenderSidebarFull(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("href=\"/guide/intro.html\"");
        await Assert.That(html).DoesNotContain("href=\"/guide/index.html\"");
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
        NavPlugin plugin = new(options);
        BuildDiscoverContext discoverContext = new(fixture.Root, fixture.Output, [plugin]) { UseDirectoryUrls = true };
        await plugin.DiscoverAsync(discoverContext, CancellationToken.None);

        var html = RunPostRender(plugin, "guide/index.md");
        await Assert.That(html).Contains("Home");
        await Assert.That(html).Contains("href=\"/guide/intro/\"");
        await Assert.That(html).DoesNotContain(">Guide<");
        await Assert.That(html).DoesNotContain("href=\"/blog/post/\"");
    }

    /// <summary>Sidebar sections without an index page link to the first real descendant page.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidebarSectionWithoutIndexUsesFirstDescendantHref()
    {
        NavNode blogs = new("Blogs", "docs/resources/blogs.md", isSection: false, [], useDirectoryUrls: true);
        NavNode videos = new("Videos", "docs/resources/videos.md", isSection: false, [], useDirectoryUrls: true);
        NavNode resources = new("Resources", "docs/resources", isSection: true, [blogs, videos], useDirectoryUrls: true);
        NavNode root = new(string.Empty, string.Empty, isSection: true, [resources], useDirectoryUrls: true);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, resources);
        NavRenderer.RenderSidebarPruned(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("href=\"/docs/resources/blogs/\"");
        await Assert.That(html).DoesNotContain("href=\"/docs/resources/\"");
    }

    /// <summary>The flattened contextual sidebar omits the active section wrapper entirely.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidebarOmitsRedundantNestedSectionTitle()
    {
        NavNode intro = new("Getting Started", "docs/getting-started/index.md", isSection: false, [], useDirectoryUrls: true);
        NavNode docs = new("Docs", "docs", isSection: true, [intro], indexPath: "docs/index.md", useDirectoryUrls: true);
        NavNode root = new(string.Empty, string.Empty, isSection: true, [docs], useDirectoryUrls: true);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, docs);
        NavRenderer.RenderSidebarPruned(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<span class=\"md-ellipsis\">Getting Started</span>");
        await Assert.That(html).DoesNotContain("<span class=\"md-ellipsis\">Docs</span>");
    }

    /// <summary>
    /// When the active page is a section's own index (e.g. <c>api/index.md</c>) and the section
    /// has many child sub-sections (typical API-reference shape), the sidebar should render the
    /// active section as an expandable header (<c>--active --section --nested</c>) with its
    /// children listed inside a nested <c>&lt;nav data-md-level="1"&gt;</c> wrapper, matching the
    /// standard Material drawer tree-view shape. Without this, readers see a flat list of
    /// sub-sections with no indication of which section they're inside.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidebarKeepsActiveSectionHeaderWhenSectionHasManyChildren()
    {
        NavNode akavache = new("Akavache", "api/Akavache", isSection: true, [], indexPath: "api/Akavache/index.md");
        NavNode core = new("Akavache.Core", "api/Akavache.Core", isSection: true, [], indexPath: "api/Akavache.Core/index.md");
        NavNode drawing = new("Akavache.Drawing", "api/Akavache.Drawing", isSection: true, [], indexPath: "api/Akavache.Drawing/index.md");
        NavNode api = new("API", "api", isSection: true, [akavache, core, drawing], indexPath: "api/index.md");
        NavNode root = new(string.Empty, string.Empty, isSection: true, [api]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, api);
        NavRenderer.RenderSidebarPruned(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);

        // Active section gets the expandable Material drawer shape.
        await Assert.That(html).Contains("md-nav__item--active md-nav__item--section md-nav__item--nested");

        // Children render inside a nested <nav data-md-level="1"> below the section header rather
        // than being hoisted up as flat siblings.
        await Assert.That(html).Contains("data-md-level=\"1\"");

        // Every child namespace is present in the rendered nav.
        await Assert.That(html).Contains(">Akavache<");
        await Assert.That(html).Contains(">Akavache.Core<");
        await Assert.That(html).Contains(">Akavache.Drawing<");
    }

    /// <summary>
    /// Active sections emit the full Material drawer expandable shape: a hidden checkbox toggle
    /// (pre-checked because it's on the active branch), a link + chevron-label container, and a
    /// nested <c>&lt;nav&gt;</c> with its children. CSS drives expand/collapse from the
    /// checkbox's <c>:checked</c> state — readers can collapse the active section by clicking
    /// the chevron without any JS.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ActiveSectionEmitsToggleAndContainerStructure()
    {
        NavNode akavache = new("Akavache", "api/Akavache", isSection: true, [], indexPath: "api/Akavache/index.md");
        NavNode core = new("Akavache.Core", "api/Akavache.Core", isSection: true, [], indexPath: "api/Akavache.Core/index.md");
        NavNode api = new("API", "api", isSection: true, [akavache, core], indexPath: "api/index.md");
        NavNode root = new(string.Empty, string.Empty, isSection: true, [api]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, api);
        NavRenderer.RenderSidebarPruned(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);

        // Hidden checkbox toggle, pre-checked because the section is on the active branch.
        await Assert.That(html).Contains("<input class=\"md-nav__toggle md-toggle\" type=\"checkbox\"");
        await Assert.That(html).Contains("checked>");

        // Link + chevron-label container wraps the section header.
        await Assert.That(html).Contains("<div class=\"md-nav__link md-nav__container\">");
        await Assert.That(html).Contains("<label class=\"md-nav__link md-nav__link--active\" for=\"__nav_");

        // Nested nav wires its aria-labelledby + aria-expanded to the toggle.
        await Assert.That(html).Contains("aria-labelledby=\"__nav_");
        await Assert.That(html).Contains("aria-expanded=\"true\"");

        // Drawer title inside the nested nav (visible when the sidebar opens as a mobile drawer).
        await Assert.That(html).Contains("<label class=\"md-nav__title\" for=\"__nav_");
    }

    /// <summary>
    /// Non-active sibling sections in prune mode keep the leaf-style chevron link (no toggle / no
    /// nested nav) so the rendered bytes stay small. Once the reader navigates into one, that
    /// section becomes active and gets its full expandable shape.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonActiveSectionsKeepLeafChevronShape()
    {
        NavNode intro = new("Intro", "guide/intro.md", isSection: false, []);
        NavNode post = new("Post", "blog/post.md", isSection: false, []);
        NavNode guide = new("Guide", "guide", isSection: true, [intro], indexPath: "guide/index.md");
        NavNode blog = new("Blog", "blog", isSection: true, [post], indexPath: "blog/index.md");
        NavNode root = new(string.Empty, string.Empty, isSection: true, [guide, blog]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, intro);
        NavRenderer.RenderPruned(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);

        // The active "Guide" section gets the toggle.
        await Assert.That(html).Contains("md-nav__toggle md-toggle");

        // The non-active "Blog" section's anchor exists with a leaf-style chevron icon, but its
        // children aren't rendered inline (prune mode).
        await Assert.That(html).DoesNotContain(">Post<");
        await Assert.That(html).Contains(">Blog<");
        await Assert.That(html).Contains("md-nav__icon md-icon");
    }

    /// <summary>Toggle IDs are unique within a single render so multiple sections don't collide.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToggleIdsAreUniquePerRender()
    {
        NavNode aIntro = new("A intro", "a/intro.md", isSection: false, []);
        NavNode bIntro = new("B intro", "b/intro.md", isSection: false, []);
        NavNode a = new("A", "a", isSection: true, [aIntro], indexPath: "a/index.md");
        NavNode b = new("B", "b", isSection: true, [bIntro], indexPath: "b/index.md");
        NavNode root = new(string.Empty, string.Empty, isSection: true, [a, b]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, aIntro);
        NavRenderer.RenderFull(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);

        // Two sections with children → two unique toggle IDs.
        await Assert.That(html).Contains("id=\"__nav_1\"");
        await Assert.That(html).Contains("id=\"__nav_2\"");
    }

    /// <summary>
    /// Same expandable-section shape applies under <see cref="NavOptions.Tabs"/> mode when the
    /// active top-level section has many sub-section children — the sidebar wrapping the
    /// children gives readers a sense of place even when 100 sub-sections are listed.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidebarTabsModeKeepsActiveSectionHeaderForSubSections()
    {
        NavNode home = new("Home", "index.md", isSection: false, []);
        NavNode akavache = new("Akavache", "api/Akavache", isSection: true, [], indexPath: "api/Akavache/index.md");
        NavNode core = new("Akavache.Core", "api/Akavache.Core", isSection: true, [], indexPath: "api/Akavache.Core/index.md");
        NavNode api = new("API", "api", isSection: true, [akavache, core], indexPath: "api/index.md");
        NavNode root = new(string.Empty, string.Empty, isSection: true, [home, api]);
        root.AttachParents();

        ArrayBufferWriter<byte> writer = new();
        var (tree, activeIdx) = NavTreeFlattener.FlattenWithActive(root, api);
        NavRenderer.RenderSidebarPruned(tree, activeIdx, writer);

        var html = Encoding.UTF8.GetString(writer.WrittenSpan);

        // Home leaf still sits above the active section.
        await Assert.That(html).Contains(">Home<");

        // Active section header with the expandable tree-view shape.
        await Assert.That(html).Contains("md-nav__item--active md-nav__item--section md-nav__item--nested");
        await Assert.That(html).Contains("data-md-level=\"1\"");

        // Sub-section children appear inside the section's nested nav.
        await Assert.That(html).Contains(">Akavache<");
        await Assert.That(html).Contains(">Akavache.Core<");
    }

    /// <summary>BuildUrlIndex maps every leaf URL to its node.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlIndexMapsEveryLeaf()
    {
        var root = BuildSampleTree();
        var tree = NavTreeFlattener.Flatten(root);
        var index = NavRenderer.BuildUrlIndex(tree);

        await Assert.That(index.ContainsKeyByUtf8("guide/intro.html"u8)).IsTrue();
        await Assert.That(index.ContainsKeyByUtf8("blog/post.html"u8)).IsTrue();
        await Assert.That(index.TryGetValueByUtf8("guide/intro.html"u8, out var introIdx) && Encoding.UTF8.GetString(tree.Nodes[introIdx].Title) == "Intro").IsTrue();
    }

    /// <summary>BuildUrlIndex includes section index URLs (when present).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlIndexIncludesSectionIndex()
    {
        NavNode leaf = new("Intro", "guide/intro.md", isSection: false, []);

        // Sections expose IndexUrl via the 5-arg ctor's indexPath argument.
        NavNode section = new("Guide", relativePath: string.Empty, isSection: true, [leaf], indexPath: "guide/index.md");
        NavNode root = new(string.Empty, string.Empty, isSection: true, [section]);
        var tree = NavTreeFlattener.Flatten(root);

        var index = NavRenderer.BuildUrlIndex(tree);

        await Assert.That(index.ContainsKeyByUtf8("guide/intro.html"u8)).IsTrue();
        await Assert.That(index.ContainsKeyByUtf8("guide/index.html"u8)).IsTrue();
        await Assert.That(index.TryGetValueByUtf8("guide/index.html"u8, out var guideIdx) && Encoding.UTF8.GetString(tree.Nodes[guideIdx].Title) == "Guide").IsTrue();
    }

    /// <summary>BuildUrlIndex returns an empty dictionary for an empty tree.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlIndexEmptyTree()
    {
        NavNode root = new(string.Empty, string.Empty, isSection: true, []);
        var tree = NavTreeFlattener.Flatten(root);
        var index = NavRenderer.BuildUrlIndex(tree);
        await Assert.That(index.Count).IsEqualTo(0);
    }

    /// <summary>BuildUrlIndex throws when tree is null.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildUrlIndexNullTreeThrows() =>
        await Assert.That(static () => NavRenderer.BuildUrlIndex(null!)).Throws<ArgumentNullException>();

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
        NavPlugin plugin = new(options);
        BuildDiscoverContext discoverContext = new(fixture.Root, fixture.Output, [plugin]);
        await plugin.DiscoverAsync(discoverContext, CancellationToken.None);

        var sourcePath = currentPage.Replace(".html", ".md", StringComparison.Ordinal);
        return RunPostRender(plugin, sourcePath);
    }

    /// <summary>Drives one PostRender call against a synthesized "themed" payload (marker between nav tags) and returns the rewritten HTML.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="sourcePath">Source-relative markdown path for the active page.</param>
    /// <returns>Rewritten HTML.</returns>
    private static string RunPostRender(NavPlugin plugin, string sourcePath)
    {
        ArrayBufferWriter<byte> input = new();
        input.Write("<nav>"u8);
        input.Write(NavPlugin.NavMarker);
        input.Write("</nav>"u8);

        ArrayBufferWriter<byte> output = new();
        PagePostRenderContext ctx = new(sourcePath, default, input.WrittenSpan, output);
        plugin.PostRender(in ctx);
        return Encoding.UTF8.GetString(output.WrittenSpan);
    }

    /// <summary>Builds a small two-section tree used by the index-positive tests.</summary>
    /// <returns>Tree root.</returns>
    private static NavNode BuildSampleTree()
    {
        NavNode intro = new("Intro", "guide/intro.md", isSection: false, []);
        NavNode post = new("Post", "blog/post.md", isSection: false, []);
        NavNode guide = new("Guide", string.Empty, isSection: true, [intro]);
        NavNode blog = new("Blog", string.Empty, isSection: true, [post]);
        return new(string.Empty, string.Empty, isSection: true, [guide, blog]);
    }
}
