// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Behaviour tests for <c>WyamFrontmatterReader</c>.</summary>
public class WyamFrontmatterReaderTests
{
    /// <summary>Parses the rxui Announcements frontmatter shape.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParsesRxUiAnnouncementShape()
    {
        const string Source =
            "---\nNoTitle: true\nIsBlog: true\nTitle: Memory Leak Detection\nTags: Announcement\nAuthor: Geoffrey Huntley\nPublished: 2018-05-15\n---\nBody.";
        var fm = WyamFrontmatterReader.Parse(Source);

        await Assert.That(fm.Title).IsEqualTo("Memory Leak Detection");
        await Assert.That(fm.Author).IsEqualTo("Geoffrey Huntley");
        await Assert.That(fm.Published).IsEqualTo(new(2018, 5, 15));
        await Assert.That(fm.IsBlog).IsTrue();
        await Assert.That(fm.Tags.Count).IsEqualTo(1);
        await Assert.That(fm.Tags[0]).IsEqualTo("Announcement");
    }

    /// <summary>Multiple comma-separated tags split into entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SplitsCommaSeparatedTags()
    {
        const string Source = "---\nTitle: Post\nTags: Release, Announcement, Performance\n---\nBody.";
        var fm = WyamFrontmatterReader.Parse(Source);

        await Assert.That(fm.Tags.Count).IsEqualTo(3);
        await Assert.That(fm.Tags[0]).IsEqualTo("Release");
        await Assert.That(fm.Tags[1]).IsEqualTo("Announcement");
        await Assert.That(fm.Tags[2]).IsEqualTo("Performance");
    }

    /// <summary>Source without frontmatter yields a default result.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SourceWithoutFrontmatterReturnsEmpty()
    {
        var fm = WyamFrontmatterReader.Parse("# Hello\nBody.");
        await Assert.That(fm.Title).IsEqualTo(string.Empty);
        await Assert.That(fm.BodyStartOffset).IsEqualTo(0);
    }

    /// <summary>The body offset points past the closing delimiter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BodyOffsetPointsPastClosingDelimiter()
    {
        const string Source = "---\nTitle: Hello\n---\nBody starts here.";
        var fm = WyamFrontmatterReader.Parse(Source);
        await Assert.That(Source[fm.BodyStartOffset..]).IsEqualTo("Body starts here.");
    }
}
