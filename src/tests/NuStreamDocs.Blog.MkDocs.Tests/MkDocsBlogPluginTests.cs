// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Blog.MkDocs.Tests;

/// <summary>End-to-end tests for <c>MkDocsBlogPlugin</c>.</summary>
public class MkDocsBlogPluginTests
{
    /// <summary>The mkdocs variant pulls posts from <c>{blog}/posts/</c> and emits archives under <c>{blog}/category/</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UsesPostsAndCategoryDirectories()
    {
        var docsRoot = Path.Combine(
            Path.GetTempPath(),
            "smd-mkblog-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var blogRoot = Path.Combine(docsRoot, "blog");
        var postsRoot = Path.Combine(blogRoot, "posts");
        Directory.CreateDirectory(postsRoot);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(postsRoot, "2024-01-15-launch.md"),
                "---\nTitle: Launch\nAuthor: Team\nTags: Release\nPublished: 2024-01-15\n---\nLaunch announcement.");

            MkDocsBlogPlugin plugin = new(new("blog", [.. "Blog"u8]));
            SyntheticPageSink sink = new();
            BuildDiscoverContext ctx = new(docsRoot, "/out", [], sink);
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            // Source folder must remain untouched.
            await Assert.That(File.Exists(Path.Combine(blogRoot, "index.md"))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(blogRoot, "category"))).IsFalse();

            var pages = sink.Snapshot();
            var index = Encoding.UTF8.GetString(
                pages.Single(p => p.RelativePath.Value == "blog/index.md").MarkdownBytes);
            await Assert.That(index.Contains("Launch", StringComparison.Ordinal)).IsTrue();

            var archive = Encoding.UTF8.GetString(pages.Single(p => p.RelativePath.Value == "blog/category/release.md")
                .MarkdownBytes);
            await Assert.That(archive.Contains("Launch", StringComparison.Ordinal)).IsTrue();
        }
        finally
        {
            Directory.Delete(docsRoot, true);
        }
    }

    /// <summary><c>DiscoverAsync</c> publishes the blog index's nav metadata (path, title, order) so the nav plugin can title/order the section.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PublishesBlogIndexNavEntry()
    {
        var docsRoot = Path.Combine(
            Path.GetTempPath(),
            "smd-mkblognav-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(Path.Combine(docsRoot, "blog", "posts"));

        try
        {
            MkDocsBlogPlugin plugin = new(new("blog", [.. "News"u8], false, 5));
            BuildDiscoverContext ctx = new(docsRoot, "/out", [], new());
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            await Assert.That(plugin.SyntheticNavEntries.Count).IsEqualTo(1);
            var entry = plugin.SyntheticNavEntries[0];
            await Assert.That(entry.RelativePath.Value).IsEqualTo("blog/index.md");
            await Assert.That(Encoding.UTF8.GetString(entry.Title!)).IsEqualTo("News");
            await Assert.That(entry.Order).IsEqualTo(5);
            await Assert.That(entry.Hidden).IsFalse();
        }
        finally
        {
            Directory.Delete(docsRoot, true);
        }
    }
}
