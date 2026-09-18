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
    /// <summary>The AnnouncementsName fixture value.</summary>
    private const string AnnouncementsName = "Announcements";

    /// <summary>The AnnouncementsIndexPath fixture value.</summary>
    private const string AnnouncementsIndexPath = "Announcements/index.md";

    /// <summary>Gets the AnnouncementsName fixture value.</summary>
    private static ReadOnlySpan<byte> AnnouncementsNameBytes => "Announcements"u8;

    /// <summary>OnConfigure scans the posts directory and writes index + tag archive.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsIndexAndTagArchives()
    {
        var docsRoot = Path.Combine(
            Path.GetTempPath(),
            $"smd-blog-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        var postsDir = Path.Combine(docsRoot, AnnouncementsName);
        _ = Directory.CreateDirectory(postsDir);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2018-05-15-memory-leaks.md"),
                "---\nTitle: Memory Leaks\nAuthor: Geoffrey Huntley\nTags: Announcement\nPublished: 2018-05-15\n---\nFirst paragraph.\n\nSecond paragraph.");
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2021-01-04-association.md"),
                "---\nTitle: Association\nAuthor: Rodney Littles, II\nTags: Announcement, Release\nPublished: 2021-01-04\n---\nThe announcement.");

            WyamBlogPlugin plugin = new(new(AnnouncementsName, [.. AnnouncementsNameBytes]));
            SyntheticPageSink sink = new();
            BuildDiscoverContext ctx = new(docsRoot, "/out", [], sink);
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            // Source folder must remain untouched — pages flow through the sink, not disk.
            await Assert.That(File.Exists(Path.Combine(postsDir, "index.md"))).IsFalse();
            await Assert.That(Directory.Exists(Path.Combine(postsDir, "tags"))).IsFalse();

            var pages = sink.Snapshot();
            var index = await ReadSinglePageAsync(pages, AnnouncementsIndexPath);
            await Assert.That(index.Contains("# Announcements", StringComparison.Ordinal)).IsTrue();
            await Assert.That(index.Contains("Association", StringComparison.Ordinal)).IsTrue();

            var releaseArchive = await ReadSinglePageAsync(pages, "Announcements/tags/release.md");
            await Assert.That(releaseArchive.Contains("Association", StringComparison.Ordinal)).IsTrue();
            await Assert.That(releaseArchive.Contains("Memory Leaks", StringComparison.Ordinal)).IsFalse();
        }
        finally
        {
            Directory.Delete(docsRoot, true);
        }
    }

    /// <summary>Posts are listed newest first in the index.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IndexOrdersNewestFirst()
    {
        var docsRoot = Path.Combine(
            Path.GetTempPath(),
            $"smd-blog-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        var postsDir = Path.Combine(docsRoot, "blog");
        _ = Directory.CreateDirectory(postsDir);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2020-01-01-old.md"),
                "---\nTitle: Old\nPublished: 2020-01-01\n---\nOld body.");
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2024-01-01-new.md"),
                "---\nTitle: New\nPublished: 2024-01-01\n---\nNew body.");

            WyamBlogPlugin plugin = new(new("blog", [.. "Blog"u8], false));
            SyntheticPageSink sink = new();
            BuildDiscoverContext ctx = new(docsRoot, "/out", [], sink);
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            await Assert.That(File.Exists(Path.Combine(postsDir, "index.md"))).IsFalse();
            var index = await ReadSinglePageAsync(sink.Snapshot(), "blog/index.md");
            var newPos = index.IndexOf("New", StringComparison.Ordinal);
            var oldPos = index.IndexOf("Old", StringComparison.Ordinal);
            await Assert.That(newPos).IsGreaterThan(0);
            await Assert.That(newPos).IsLessThan(oldPos);
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
        const int ExpectedOrder = 4;
        var docsRoot = Path.Combine(
            Path.GetTempPath(),
            $"smd-blognav-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        _ = Directory.CreateDirectory(Path.Combine(docsRoot, AnnouncementsName));

        try
        {
            WyamBlogPlugin plugin = new(new(AnnouncementsName, [.. AnnouncementsNameBytes], false, ExpectedOrder));
            BuildDiscoverContext ctx = new(docsRoot, "/out", [], new());
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            await Assert.That(plugin.SyntheticNavEntries.Count).IsEqualTo(1);
            var entry = plugin.SyntheticNavEntries[0];
            await Assert.That(entry.RelativePath.Value).IsEqualTo(AnnouncementsIndexPath);
            await Assert.That(Encoding.UTF8.GetString(entry.Title!)).IsEqualTo(AnnouncementsName);
            await Assert.That(entry.Order).IsEqualTo(ExpectedOrder);
            await Assert.That(entry.Hidden).IsFalse();
        }
        finally
        {
            Directory.Delete(docsRoot, true);
        }
    }

    /// <summary>The index entry is followed by one path-only entry per post, ascending Order with 0 = newest, so the nav lists posts newest-first.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PublishesPostNavEntriesNewestFirst()
    {
        const int ExpectedEntryCount = 4;
        const int OldestPostOrder = 2;
        var docsRoot = Path.Combine(
            Path.GetTempPath(),
            $"smd-blogpostnav-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        var postsDir = Path.Combine(docsRoot, AnnouncementsName);
        _ = Directory.CreateDirectory(postsDir);

        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2013-02-27-old.md"),
                "---\nTitle: Old\nPublished: 2013-02-27\n---\nbody");
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2020-06-15-mid.md"),
                "---\nTitle: Mid\nPublished: 2020-06-15\n---\nbody");
            await File.WriteAllTextAsync(
                Path.Combine(postsDir, "2026-05-07-new.md"),
                "---\nTitle: New\nPublished: 2026-05-07\n---\nbody");

            WyamBlogPlugin plugin = new(new(AnnouncementsName, [.. AnnouncementsNameBytes], false));
            BuildDiscoverContext ctx = new(docsRoot, "/out", [], new());
            await plugin.DiscoverAsync(ctx, CancellationToken.None);

            var entries = plugin.SyntheticNavEntries;
            await Assert.That(entries.Count).IsEqualTo(ExpectedEntryCount);
            await Assert.That(entries[0].RelativePath.Value).IsEqualTo(AnnouncementsIndexPath);

            // Posts follow, newest first, with ascending Order and no title (the disk page keeps its own).
            await Assert.That(entries[1].RelativePath.Value).IsEqualTo("Announcements/2026-05-07-new.md");
            await Assert.That(entries[1].Order).IsEqualTo(0);
            await Assert.That(entries[1].Title).IsNull();
            await Assert.That(entries[2].RelativePath.Value).IsEqualTo("Announcements/2020-06-15-mid.md");
            await Assert.That(entries[2].Order).IsEqualTo(1);
            await Assert.That(entries[3].RelativePath.Value).IsEqualTo("Announcements/2013-02-27-old.md");
            await Assert.That(entries[3].Order).IsEqualTo(OldestPostOrder);
        }
        finally
        {
            Directory.Delete(docsRoot, true);
        }
    }

    /// <summary>Reads the unique synthetic page with the requested path.</summary>
    /// <param name="pages">Generated pages.</param>
    /// <param name="relativePath">Requested source-relative path.</param>
    /// <returns>The page's markdown text.</returns>
    private static async Task<string> ReadSinglePageAsync(SyntheticPage[] pages, string relativePath)
    {
        var match = default(SyntheticPage);
        var count = 0;
        for (var i = 0; i < pages.Length; i++)
        {
            if (!string.Equals(pages[i].RelativePath.Value, relativePath, StringComparison.Ordinal))
            {
                continue;
            }

            match = pages[i];
            count++;
        }

        await Assert.That(count).IsEqualTo(1);
        return Encoding.UTF8.GetString(match.MarkdownBytes);
    }
}
