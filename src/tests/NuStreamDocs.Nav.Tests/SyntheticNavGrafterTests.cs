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
    /// <summary>An empty entry list returns the same root instance untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyEntriesReturnsSameRoot()
    {
        var root = Root(Section("documentation", "documentation/index.md"));
        var result = SyntheticNavGrafter.Graft(root, [], useDirectoryUrls: true);
        await Assert.That(result).IsSameReferenceAs(root);
    }

    /// <summary>A <c>seg/index.md</c> entry grafts a new top-level section carrying that index, title, and order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndexEntryGraftsSection()
    {
        var root = Root(Section("documentation", "documentation/index.md"));
        SyntheticNavEntry entry = new((FilePath)"api/index.md", [.. "API Reference"u8], 2, Hidden: false);

        var result = SyntheticNavGrafter.Graft(root, [entry], useDirectoryUrls: true);

        var api = FindChild(result, "api");
        await Assert.That(api).IsNotNull();
        await Assert.That(api!.IsSection).IsTrue();
        await Assert.That(api.IndexPath.Value).IsEqualTo("api/index.md");
        await Assert.That(Encoding.UTF8.GetString(api.Title)).IsEqualTo("API Reference");
        await Assert.That(api.Order).IsEqualTo(2);
    }

    /// <summary>When a disk section already has an index page, the synthetic entry is ignored — disk wins.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CompleteDiskSectionWins()
    {
        var root = Root(Section("api", "api/index.md", "Hand-written API"));
        SyntheticNavEntry entry = new((FilePath)"api/index.md", [.. "Generated API"u8], 2, Hidden: false);

        var result = SyntheticNavGrafter.Graft(root, [entry], useDirectoryUrls: true);

        await Assert.That(result).IsSameReferenceAs(root);
        await Assert.That(Encoding.UTF8.GetString(FindChild(result, "api")!.Title)).IsEqualTo("Hand-written API");
    }

    /// <summary>A disk section that lacks an index page picks up the synthetic index, title, and order, keeping its disk children.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IncompleteDiskSectionMergesSyntheticIndex()
    {
        // Mirrors the blog case: docs/articles/ has post .md files but no index.md on disk.
        var posts = new NavNode("2025-01-01-post", (FilePath)"articles/2025-01-01-post.md", isSection: false, [], useDirectoryUrls: true);
        var diskArticles = new NavNode("articles", (FilePath)"articles", isSection: true, [posts], default, useDirectoryUrls: true);
        var root = Root(diskArticles);
        SyntheticNavEntry entry = new((FilePath)"articles/index.md", [.. "Release Notes"u8], 3, Hidden: false);

        var result = SyntheticNavGrafter.Graft(root, [entry], useDirectoryUrls: true);
        var articles = FindChild(result, "articles")!;

        await Assert.That(articles.IndexPath.Value).IsEqualTo("articles/index.md");
        await Assert.That(Encoding.UTF8.GetString(articles.Title)).IsEqualTo("Release Notes");
        await Assert.That(articles.Order).IsEqualTo(3);
        await Assert.That(articles.Children.Length).IsEqualTo(1);
        await Assert.That(articles.Children[0].RelativePath.Value).IsEqualTo("articles/2025-01-01-post.md");
    }

    /// <summary>A hidden index entry produces no section.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HiddenEntryProducesNoSection()
    {
        var root = Root(Section("documentation", "documentation/index.md"));
        SyntheticNavEntry entry = new((FilePath)"api/index.md", [.. "API"u8], null, Hidden: true);

        var result = SyntheticNavGrafter.Graft(root, [entry], useDirectoryUrls: true);

        await Assert.That(result).IsSameReferenceAs(root);
    }

    /// <summary>Nested entries build a section → subsection → page chain.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedEntriesBuildSubtree()
    {
        var root = Root();
        SyntheticNavEntry[] entries =
        [
            new((FilePath)"api/index.md", [.. "API Reference"u8], 2, Hidden: false),
            new((FilePath)"api/ReactiveUI/index.md", [.. "ReactiveUI"u8], null, Hidden: false),
            new((FilePath)"api/ReactiveUI/ReactiveCommand.md", [.. "ReactiveCommand"u8], null, Hidden: false),
        ];

        var result = SyntheticNavGrafter.Graft(root, entries, useDirectoryUrls: true);

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
        var root = Root(
            Section("documentation", "documentation/index.md"),
            Section("vs", "vs/index.md"));
        SyntheticNavEntry entry = new((FilePath)"api/index.md", [.. "API Reference"u8], 1, Hidden: false);

        var result = SyntheticNavGrafter.Graft(root, [entry], useDirectoryUrls: true);

        // Order 1 < int.MaxValue, so api comes first; the unordered disk sections keep alpha order after it.
        await Assert.That(result.Children.Length).IsEqualTo(3);
        await Assert.That(result.Children[0].RelativePath.Value).IsEqualTo("api");
        await Assert.That(result.Children[1].RelativePath.Value).IsEqualTo("documentation");
        await Assert.That(result.Children[2].RelativePath.Value).IsEqualTo("vs");
    }

    /// <summary>Builds a root section node with the supplied children.</summary>
    /// <param name="children">Child nodes.</param>
    /// <returns>The root node.</returns>
    private static NavNode Root(params NavNode[] children) =>
        new([], default, isSection: true, children, default, useDirectoryUrls: true);

    /// <summary>Builds a section node.</summary>
    /// <param name="name">Directory name (also the relative path).</param>
    /// <param name="indexRel">Relative path of the section's index page.</param>
    /// <param name="title">Display title; defaults to <paramref name="name"/>.</param>
    /// <returns>The section node.</returns>
    private static NavNode Section(string name, string indexRel, string? title = null) =>
        new(title ?? name, (FilePath)name, isSection: true, [], (FilePath)indexRel, useDirectoryUrls: true);

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
