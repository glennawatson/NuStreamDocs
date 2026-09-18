// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Blog.Common;

namespace NuStreamDocs.Blog.Tests;

/// <summary>Behavior tests for <c>WyamFrontmatterReader</c>.</summary>
public class WyamFrontmatterReaderTests
{
    /// <summary>Year in the sample announcement's publication date.</summary>
    private const int AnnouncementYear = 2018;

    /// <summary>Month in the sample announcement's publication date.</summary>
    private const int AnnouncementMonth = 5;

    /// <summary>Day in the sample announcement's publication date.</summary>
    private const int AnnouncementDay = 15;

    /// <summary>Gets the publication date of the sample announcement.</summary>
    private static DateOnly AnnouncementDate => new(AnnouncementYear, AnnouncementMonth, AnnouncementDay);

    /// <summary>Parses the rxui Announcements frontmatter shape.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ParsesRxUiAnnouncementShape()
    {
        var fm = WyamFrontmatterReader.Parse(
            "---\nNoTitle: true\nIsBlog: true\nTitle: Memory Leak Detection\nTags: Announcement\nAuthor: Geoffrey Huntley\nPublished: 2018-05-15\n---\nBody."u8);

        await Assert.That(fm.Title.AsSpan().SequenceEqual("Memory Leak Detection"u8)).IsTrue();
        await Assert.That(fm.Author.AsSpan().SequenceEqual("Geoffrey Huntley"u8)).IsTrue();
        await Assert.That(fm.Published).IsEqualTo(AnnouncementDate);
        await Assert.That(fm.IsBlog).IsTrue();
        await Assert.That(fm.Tags.Length).IsEqualTo(1);
        await Assert.That(fm.Tags[0].AsSpan().SequenceEqual("Announcement"u8)).IsTrue();
    }

    /// <summary>Multiple comma-separated tags split into entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SplitsCommaSeparatedTags()
    {
        const int ExpectedTagCount = 3;
        var fm = WyamFrontmatterReader.Parse(
            "---\nTitle: Post\nTags: Release, Announcement, Performance\n---\nBody."u8);

        await Assert.That(fm.Tags.Length).IsEqualTo(ExpectedTagCount);
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
        byte[] source = [.. "---\nTitle: Hello\n---\nBody starts here."u8];
        var fm = WyamFrontmatterReader.Parse(source);
        await Assert.That(Encoding.UTF8.GetString(source.AsSpan(fm.BodyStartOffset))).IsEqualTo("Body starts here.");
    }

    /// <summary>Published dates accept valid ISO dates and reject malformed or oversized values.</summary>
    /// <param name="value">Published frontmatter value.</param>
    /// <param name="year">Expected year.</param>
    /// <param name="month">Expected month.</param>
    /// <param name="day">Expected day.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("2024-02-29", 2024, 2, 29)]
    [Arguments("2023-02-29", 1, 1, 1)]
    [Arguments("2026-1-01", 1, 1, 1)]
    [Arguments("2026-01-01-extra", 1, 1, 1)]
    [Arguments("２０２６-01-01", 1, 1, 1)]
    public async Task PublishedDateRequiresExactIsoFormat(string value, int year, int month, int day)
    {
        var source = Encoding.UTF8.GetBytes($"---\nPublished: {value}\n---\n");
        var parsed = WyamFrontmatterReader.Parse(source);
        await Assert.That(parsed.Published).IsEqualTo(new(year, month, day));
    }
}
