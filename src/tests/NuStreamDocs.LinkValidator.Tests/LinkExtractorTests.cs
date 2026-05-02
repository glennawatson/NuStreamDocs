// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>Behavior tests for the byte-only LinkExtractor.</summary>
public class LinkExtractorTests
{
    /// <summary>Every <c>href</c> attribute on the page is captured as a byte range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsEveryHref()
    {
        byte[] html = [.. "<a href=\"a.html\">a</a> <a href=\"https://example.com\">b</a>"u8];
        var ranges = LinkExtractor.ExtractHrefRanges(html);
        await Assert.That(ranges.Length).IsEqualTo(2);
        await Assert.That(SliceEquals(html, ranges[0], "a.html"u8)).IsTrue();
        await Assert.That(SliceEquals(html, ranges[1], "https://example.com"u8)).IsTrue();
    }

    /// <summary>Every <c>src</c> attribute on the page is captured.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsEverySrc()
    {
        byte[] html = [.. "<img src=\"x.png\"/><img src=\"https://cdn.test/y.jpg\"/>"u8];
        var ranges = LinkExtractor.ExtractSrcRanges(html);
        await Assert.That(ranges.Length).IsEqualTo(2);
        await Assert.That(SliceEquals(html, ranges[0], "x.png"u8)).IsTrue();
        await Assert.That(SliceEquals(html, ranges[1], "https://cdn.test/y.jpg"u8)).IsTrue();
    }

    /// <summary>Heading <c>id</c> attributes are captured but non-heading id attrs are skipped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsHeadingIdsOnly()
    {
        byte[] html = [.. "<h1 id=\"intro\">Intro</h1><p id=\"para\">body</p><h2 id=\"detail\">x</h2>"u8];
        var ranges = LinkExtractor.ExtractHeadingIdRanges(html);
        await Assert.That(ranges.Length).IsEqualTo(2);
        await Assert.That(SliceEquals(html, ranges[0], "intro"u8)).IsTrue();
        await Assert.That(SliceEquals(html, ranges[1], "detail"u8)).IsTrue();
    }

    /// <summary>Empty input returns no ranges.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmptyInput()
    {
        await Assert.That(LinkExtractor.ExtractHrefRanges([]).Length).IsEqualTo(0);
        await Assert.That(LinkExtractor.ExtractSrcRanges([]).Length).IsEqualTo(0);
        await Assert.That(LinkExtractor.ExtractHeadingIdRanges([]).Length).IsEqualTo(0);
    }

    /// <summary>Resolves <paramref name="range"/> against <paramref name="source"/> and tests byte-equality with <paramref name="expected"/>.</summary>
    /// <param name="source">Source bytes.</param>
    /// <param name="range">Captured range.</param>
    /// <param name="expected">Expected bytes.</param>
    /// <returns>True when the slice equals the expected span.</returns>
    private static bool SliceEquals(byte[] source, in ByteRange range, ReadOnlySpan<byte> expected) =>
        range.AsSpan(source).SequenceEqual(expected);
}
