// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Behavior tests for <c>WyamFrontmatterReader</c>.</summary>
public class WyamFrontmatterReaderTests
{
    /// <summary>Parses the rxui Announcements frontmatter shape.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParsesRxUiAnnouncementShape()
    {
        var fm = WyamFrontmatterReader.Parse(
            "---\nNoTitle: true\nIsBlog: true\nTitle: Memory Leak Detection\nTags: Announcement\nAuthor: Geoffrey Huntley\nPublished: 2018-05-15\n---\nBody."u8);

        await Assert.That(fm.Title.AsSpan().SequenceEqual("Memory Leak Detection"u8)).IsTrue();
        await Assert.That(fm.Author.AsSpan().SequenceEqual("Geoffrey Huntley"u8)).IsTrue();
        await Assert.That(fm.Published).IsEqualTo(new(2018, 5, 15));
        await Assert.That(fm.IsBlog).IsTrue();
        await Assert.That(fm.Tags.Length).IsEqualTo(1);
        await Assert.That(fm.Tags[0].AsSpan().SequenceEqual("Announcement"u8)).IsTrue();
    }

    /// <summary>Multiple comma-separated tags split into entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SplitsCommaSeparatedTags()
    {
        var fm = WyamFrontmatterReader.Parse(
            "---\nTitle: Post\nTags: Release, Announcement, Performance\n---\nBody."u8);

        await Assert.That(fm.Tags.Length).IsEqualTo(3);
        await Assert.That(fm.Tags[0].AsSpan().SequenceEqual("Release"u8)).IsTrue();
        await Assert.That(fm.Tags[1].AsSpan().SequenceEqual("Announcement"u8)).IsTrue();
        await Assert.That(fm.Tags[2].AsSpan().SequenceEqual("Performance"u8)).IsTrue();
    }

    /// <summary>Source without frontmatter yields a default result.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SourceWithoutFrontmatterReturnsEmpty()
    {
        var fm = WyamFrontmatterReader.Parse("# Hello\nBody."u8);
        await Assert.That(fm.Title.Length).IsEqualTo(0);
        await Assert.That(fm.BodyStartOffset).IsEqualTo(0);
    }

    /// <summary>The body offset points past the closing delimiter.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BodyOffsetPointsPastClosingDelimiter()
    {
        var source = "---\nTitle: Hello\n---\nBody starts here."u8.ToArray();
        var fm = WyamFrontmatterReader.Parse(source);
        await Assert.That(Encoding.UTF8.GetString(source.AsSpan(fm.BodyStartOffset))).IsEqualTo("Body starts here.");
    }
}
