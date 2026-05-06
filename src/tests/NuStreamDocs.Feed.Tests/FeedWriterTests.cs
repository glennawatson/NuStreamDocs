// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;
using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Feed.Tests;

/// <summary>Behavior tests for <c>FeedWriter</c>.</summary>
public class FeedWriterTests
{
    /// <summary>The RSS document validates as XML and lists every supplied post.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RssDocumentIncludesEveryPost()
    {
        var options = BuildOptions();
        var posts = BuildPosts();
        var bytes = FeedWriter.WriteRss(options, posts, new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var xml = Encoding.UTF8.GetString(bytes);
        XmlDocument doc = new();
        doc.LoadXml(xml);

        var titles = doc.SelectNodes("//item/title")!;
        await Assert.That(titles.Count).IsEqualTo(2);
        await Assert.That(xml.Contains("Launch", StringComparison.Ordinal)).IsTrue();
        await Assert.That(xml.Contains("Update", StringComparison.Ordinal)).IsTrue();
        await Assert.That(xml.Contains("https://example.com/blog/2024-06-01-launch.html", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>The Atom document carries the configured feed metadata and entry count.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AtomDocumentHasFeedMetadata()
    {
        var options = BuildOptions();
        var posts = BuildPosts();
        var bytes = FeedWriter.WriteAtom(options, posts, new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var xml = Encoding.UTF8.GetString(bytes);

        await Assert.That(xml.Contains("<feed", StringComparison.Ordinal)).IsTrue();
        await Assert.That(xml.Contains("Test Feed", StringComparison.Ordinal)).IsTrue();
        await Assert.That(xml.Contains("<entry>", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>The MaxItems cap clips the entry list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task MaxItemsCapsItemCount()
    {
        var options = BuildOptions() with { MaxItems = 1 };
        var posts = BuildPosts();
        var bytes = FeedWriter.WriteRss(options, posts, DateTimeOffset.UtcNow);
        var xml = Encoding.UTF8.GetString(bytes);
        XmlDocument doc = new();
        doc.LoadXml(xml);

        var items = doc.SelectNodes("//item")!;
        await Assert.That(items.Count).IsEqualTo(1);
    }

    /// <summary>Builds standard test options.</summary>
    /// <returns>Options.</returns>
    private static FeedOptions BuildOptions() =>
        new("https://example.com", "Test Feed", "A test feed", "blog");

    /// <summary>Builds two posts for the writer to consume.</summary>
    /// <returns>Posts list.</returns>
    private static BlogPost[] BuildPosts() => [
        new(
            "blog/2024-06-01-launch.md",
            [.. "blog/2024-06-01-launch.html"u8],
            [.. "launch"u8],
            [.. "Launch"u8],
            [.. "Author A"u8],
            new(2024, 6, 1),
            [[.. "Release"u8]],
            [.. "Launch announcement."u8]),
        new(
            "blog/2024-05-01-update.md",
            [.. "blog/2024-05-01-update.html"u8],
            [.. "update"u8],
            [.. "Update"u8],
            [.. "Author B"u8],
            new(2024, 5, 1),
            [[.. "Update"u8]],
            [.. "Update announcement."u8])
    ];
}
