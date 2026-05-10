// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Blog.Tests;

/// <summary>End-to-end tests for <c>WyamBlogPlugin</c>.</summary>
public class WyamBlogPluginTests
{
    /// <summary>OnConfigure scans the posts directory and writes index + tag archive.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsIndexAndTagArchives()
    {
        var docsRoot = Path.Combine(Path.GetTempPath(), "smd-blog-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var postsDir = Path.Combine(docsRoot, "Announcements");
        Directory.CreateDirectory(postsDir);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2018-05-15-memory-leaks.md"),
                "---\nTitle: Memory Leaks\nAuthor: Geoffrey Huntley\nTags: Announcement\nPublished: 2018-05-15\n---\nFirst paragraph.\n\nSecond paragraph.");
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2021-01-04-association.md"),
                "---\nTitle: Association\nAuthor: Rodney Littles, II\nTags: Announcement, Release\nPublished: 2021-01-04\n---\nThe announcement.");

            WyamBlogPlugin plugin = new(new("Announcements", [.. "Announcements"u8]));
            SyntheticPageSink sink = new();
            BuildDiscoverContext ctx = new(docsRoot, "/out", [], sink);
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            // Source folder must remain untouched — pages flow through the sink, not disk.
            await Assert.That(File.Exists(Path.Combine(postsDir, "index.md"))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(postsDir, "tags"))).IsFalse();

            var pages = sink.Snapshot();
            var index = Encoding.UTF8.GetString(pages.Single(p => p.RelativePath.Value == "Announcements/index.md").MarkdownBytes);
            await Assert.That(index.Contains("# Announcements", StringComparison.Ordinal)).IsTrue();
            await Assert.That(index.Contains("Association", StringComparison.Ordinal)).IsTrue();

            var releaseArchive = Encoding.UTF8.GetString(pages.Single(p => p.RelativePath.Value == "Announcements/tags/release.md").MarkdownBytes);
            await Assert.That(releaseArchive.Contains("Association", StringComparison.Ordinal)).IsTrue();
            await Assert.That(releaseArchive.Contains("Memory Leaks", StringComparison.Ordinal)).IsFalse();
        }
        finally
        {
            Directory.Delete(docsRoot, recursive: true);
        }
    }

    /// <summary>Posts are listed newest first in the index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IndexOrdersNewestFirst()
    {
        var docsRoot = Path.Combine(Path.GetTempPath(), "smd-blog-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var postsDir = Path.Combine(docsRoot, "blog");
        Directory.CreateDirectory(postsDir);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(postsDir, "2020-01-01-old.md"), "---\nTitle: Old\nPublished: 2020-01-01\n---\nOld body.");
            await File.WriteAllTextAsync(Path.Combine(postsDir, "2024-01-01-new.md"), "---\nTitle: New\nPublished: 2024-01-01\n---\nNew body.");

            WyamBlogPlugin plugin = new(new("blog", [.. "Blog"u8], EmitTagArchives: false));
            SyntheticPageSink sink = new();
            BuildDiscoverContext ctx = new(docsRoot, "/out", [], sink);
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            await Assert.That(File.Exists(Path.Combine(postsDir, "index.md"))).IsFalse();
            var index = Encoding.UTF8.GetString(sink.Snapshot().Single(p => p.RelativePath.Value == "blog/index.md").MarkdownBytes);
            var newPos = index.IndexOf("New", StringComparison.Ordinal);
            var oldPos = index.IndexOf("Old", StringComparison.Ordinal);
            await Assert.That(newPos).IsGreaterThan(0);
            await Assert.That(newPos).IsLessThan(oldPos);
        }
        finally
        {
            Directory.Delete(docsRoot, recursive: true);
        }
    }
}
