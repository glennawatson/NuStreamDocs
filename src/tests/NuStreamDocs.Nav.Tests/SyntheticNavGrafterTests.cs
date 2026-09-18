// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Nav.Tests;

/// <summary>Tests for <see cref="SyntheticNavGrafter"/>.</summary>
public class SyntheticNavGrafterTests
{
    /// <summary>The ApiIndexPath fixture value.</summary>
    private const string ApiIndexPath = "api/index.md";

    /// <summary>The DocumentationDirectory fixture value.</summary>
    private const string DocumentationDirectory = "documentation";

    /// <summary>The DocumentationIndexPath fixture value.</summary>
    private const string DocumentationIndexPath = "documentation/index.md";

    /// <summary>The ApiReferenceTitle fixture value.</summary>
    private const string ApiReferenceTitle = "API Reference";

    /// <summary>The ArticlesDirectory fixture value.</summary>
    private const string ArticlesDirectory = "articles";

    /// <summary>The ArticlesIndexPath fixture value.</summary>
    private const string ArticlesIndexPath = "articles/index.md";

    /// <summary>The OlderArticlePath fixture value.</summary>
    private const string OlderArticlePath = "articles/2013-02-27-old.md";

    /// <summary>The NewerArticlePath fixture value.</summary>
    private const string NewerArticlePath = "articles/2026-05-07-new.md";

    /// <summary>Gets the ApiReferenceTitle fixture value.</summary>
    private static ReadOnlySpan<byte> ApiReferenceTitleBytes => "API Reference"u8;

    /// <summary>An empty entry list returns the same root instance untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyEntriesReturnsSameRoot()
    {
        var root = Root(Section(DocumentationDirectory, DocumentationIndexPath));
        var result = SyntheticNavGrafter.Graft(root, [], true);
        await Assert.That(result).IsSameReferenceAs(root);
    }

    /// <summary>A <c>seg/index.md</c> entry grafts a new top-level section carrying that index, title, and order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndexEntryGraftsSection()
    {
        const int ExpectedOrder = 2;
        var root = Root(Section(DocumentationDirectory, DocumentationIndexPath));
        SyntheticNavEntry entry = new((FilePath)ApiIndexPath, [.. ApiReferenceTitleBytes], ExpectedOrder, false);

        var result = SyntheticNavGrafter.Graft(root, [entry], true);

        var api = FindChild(result, "api");
        await Assert.That(api).IsNotNull();
        await Assert.That(api!.IsSection).IsTrue();
        await Assert.That(api.IndexPath.Value).IsEqualTo(ApiIndexPath);
        await Assert.That(Encoding.UTF8.GetString(api.Title)).IsEqualTo(ApiReferenceTitle);
        await Assert.That(api.Order).IsEqualTo(ExpectedOrder);
    }

    /// <summary>When a disk section already has an index page, the synthetic entry is ignored — disk wins.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CompleteDiskSectionWins()
    {
        var root = Root(Section("api", ApiIndexPath, "Hand-written API"));
        const int SyntheticOrder = 2;
        SyntheticNavEntry entry = new((FilePath)ApiIndexPath, [.. "Generated API"u8], SyntheticOrder, false);

        var result = SyntheticNavGrafter.Graft(root, [entry], true);

        await Assert.That(result).IsSameReferenceAs(root);
        await Assert.That(Encoding.UTF8.GetString(FindChild(result, "api")!.Title)).IsEqualTo("Hand-written API");
    }

    /// <summary>A disk section that lacks an index page picks up the synthetic index, title, and order, keeping its disk children.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncompleteDiskSectionMergesSyntheticIndex()
    {
        const int ExpectedOrder = 3;

        // Mirrors the blog case: docs/articles/ has post .md files but no index.md on disk.
        var posts = new NavNode("2025-01-01-post", (FilePath)"articles/2025-01-01-post.md", false, [], true);
        var diskArticles = new NavNode(ArticlesDirectory, (FilePath)ArticlesDirectory, true, [posts], default, true);
        var root = Root(diskArticles);
        SyntheticNavEntry entry = new((FilePath)ArticlesIndexPath, [.. "Release Notes"u8], ExpectedOrder, false);

        var result = SyntheticNavGrafter.Graft(root, [entry], true);
        var articles = FindChild(result, ArticlesDirectory)!;

        await Assert.That(articles.IndexPath.Value).IsEqualTo(ArticlesIndexPath);
        await Assert.That(Encoding.UTF8.GetString(articles.Title)).IsEqualTo("Release Notes");
        await Assert.That(articles.Order).IsEqualTo(ExpectedOrder);
        await Assert.That(articles.Children.Length).IsEqualTo(1);
        await Assert.That(articles.Children[0].RelativePath.Value).IsEqualTo("articles/2025-01-01-post.md");
    }

    /// <summary>A synthetic page entry transfers its Order onto the matching disk page, keeping the disk page's own title; the section re-sorts by the new Order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SyntheticPageEntryTransfersOrderOntoDiskPage()
    {
        const int ExpectedCount = 2;

        // Disk: an `articles` section with no index page, two date-prefixed posts (filename order = oldest first).
        var oldPost = new NavNode("Old Post", (FilePath)OlderArticlePath, false, [], true);
        var newPost = new NavNode("New Post", (FilePath)NewerArticlePath, false, [], true);
        var diskArticles = new NavNode(ArticlesDirectory, (FilePath)ArticlesDirectory, true, [oldPost, newPost], default, true);
        var root = Root(diskArticles);

        // Synthetic: the blog index entry plus per-post entries carrying ascending Order (0 = newest).
        const int SectionOrder = 3;
        SyntheticNavEntry[] entries =
        [
            new((FilePath)ArticlesIndexPath, [.. "Articles"u8], SectionOrder, false),
            new((FilePath)NewerArticlePath, null, 0, false),
            new((FilePath)OlderArticlePath, null, 1, false)
        ];

        var result = SyntheticNavGrafter.Graft(root, entries, true);
        var articles = FindChild(result, ArticlesDirectory)!;

        await Assert.That(articles.IndexPath.Value).IsEqualTo(ArticlesIndexPath);
        await Assert.That(articles.Children.Length).IsEqualTo(ExpectedCount);

        // Newest first (Order 0 then 1), and each post keeps its disk-frontmatter title.
        await Assert.That(articles.Children[0].RelativePath.Value).IsEqualTo(NewerArticlePath);
        await Assert.That(articles.Children[0].Order).IsEqualTo(0);
        await Assert.That(Encoding.UTF8.GetString(articles.Children[0].Title)).IsEqualTo("New Post");
        await Assert.That(articles.Children[1].RelativePath.Value).IsEqualTo(OlderArticlePath);
        await Assert.That(articles.Children[1].Order).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(articles.Children[1].Title)).IsEqualTo("Old Post");
    }

    /// <summary>A hidden index entry produces no section.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HiddenEntryProducesNoSection()
    {
        var root = Root(Section(DocumentationDirectory, DocumentationIndexPath));
        SyntheticNavEntry entry = new((FilePath)ApiIndexPath, [.. "API"u8], null, true);

        var result = SyntheticNavGrafter.Graft(root, [entry], true);

        await Assert.That(result).IsSameReferenceAs(root);
    }

    /// <summary>Nested entries build a section → subsection → page chain.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedEntriesBuildSubtree()
    {
        const int SectionOrder = 2;
        var root = Root();
        SyntheticNavEntry[] entries =
        [
            new((FilePath)ApiIndexPath, [.. ApiReferenceTitleBytes], SectionOrder, false),
            new((FilePath)"api/ReactiveUI/index.md", [.. "ReactiveUI"u8], null, false),
            new((FilePath)"api/ReactiveUI/ReactiveCommand.md", [.. "ReactiveCommand"u8], null, false)
        ];

        var result = SyntheticNavGrafter.Graft(root, entries, true);

        var api = FindChild(result, "api")!;
        await Assert.That(api.Children.Length).IsEqualTo(1);
        var ns = api.Children[0];
        await Assert.That(ns.IsSection).IsTrue();
        await Assert.That(ns.RelativePath.Value).IsEqualTo("api/ReactiveUI");
        await Assert.That(ns.IndexPath.Value).IsEqualTo("api/ReactiveUI/index.md");
        await Assert.That(ns.Children.Length).IsEqualTo(1);
        await Assert.That(ns.Children[0].IsSection).IsFalse();
        await Assert.That(ns.Children[0].RelativePath.Value).IsEqualTo("api/ReactiveUI/ReactiveCommand.md");
    }

    /// <summary>A grafted section with an explicit Order sorts ahead of unordered disk siblings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OrderedSectionSortsAmongSiblings()
    {
        const int ExpectedCount = 3;
        var root = Root(
            Section(DocumentationDirectory, DocumentationIndexPath),
            Section("vs", "vs/index.md"));
        SyntheticNavEntry entry = new((FilePath)ApiIndexPath, [.. ApiReferenceTitleBytes], 1, false);

        var result = SyntheticNavGrafter.Graft(root, [entry], true);

        // Order 1 < int.MaxValue, so api comes first; the unordered disk sections keep alpha order after it.
        await Assert.That(result.Children.Length).IsEqualTo(ExpectedCount);
        await Assert.That(result.Children[0].RelativePath.Value).IsEqualTo("api");
        await Assert.That(result.Children[1].RelativePath.Value).IsEqualTo(DocumentationDirectory);
        await Assert.That(result.Children[2].RelativePath.Value).IsEqualTo("vs");
    }

    /// <summary>Builds a root section node with the supplied children.</summary>
    /// <param name="children">Child nodes.</param>
    /// <returns>The root node.</returns>
    private static NavNode Root(params NavNode[] children) =>
        new([], default, true, children, default, true);

    /// <summary>Builds a section node.</summary>
    /// <param name="name">Directory name (also the relative path).</param>
    /// <param name="indexRel">Relative path of the section's index page.</param>
    /// <param name="title">Display title; defaults to <paramref name="name"/>.</param>
    /// <returns>The section node.</returns>
    private static NavNode Section(string name, string indexRel, string? title = null) =>
        new(title ?? name, (FilePath)name, true, [], (FilePath)indexRel, true);

    /// <summary>Returns the top-level child whose section directory name matches <paramref name="name"/>, or null.</summary>
    /// <param name="root">Root node.</param>
    /// <param name="name">Section directory name.</param>
    /// <returns>The matching child, or null.</returns>
    private static NavNode? FindChild(NavNode root, string name)
    {
        foreach (var child in root.Children)
        {
            if (child.RelativePath.Value == name)
            {
                return child;
            }
        }

        return null;
    }
}
