// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Toc.Tests;

/// <summary>Branch-coverage edge cases for HeadingScanner.</summary>
public class HeadingScannerBranchTests
{
    /// <summary>Empty input returns no headings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput() => await Assert.That(HeadingScanner.Scan([]).Length).IsEqualTo(0);

    /// <summary>Heading without a closing tag is dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeadingWithoutClose() =>
        await Assert.That(HeadingScanner.Scan("<h2>no close"u8.ToArray()).Length).IsEqualTo(0);

    /// <summary>Single-quoted id attribute is captured.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleQuotedId()
    {
        var headings = HeadingScanner.Scan("<h2 id='single'>x</h2>"u8.ToArray());
        await Assert.That(headings[0].ExistingId).IsEqualTo("single");
    }

    /// <summary>Quoted id without a closing quote yields an empty existing id.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedQuoteId()
    {
        var headings = HeadingScanner.Scan("<h2 id=\"never\">body</h2>"u8.ToArray());
        await Assert.That(headings.Length).IsEqualTo(1);
        await Assert.That(headings[0].ExistingId).IsEqualTo("never");
    }

    /// <summary>Unquoted id attribute is captured up to whitespace or close.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnquotedId()
    {
        var headings = HeadingScanner.Scan("<h2 id=plain class=x>body</h2>"u8.ToArray());
        await Assert.That(headings.Length).IsEqualTo(1);
        await Assert.That(headings[0].ExistingId).IsEqualTo("plain");
    }

    /// <summary>DecodeText with body containing an unclosed inner tag returns the prefix.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DecodeUnclosedInnerTag()
    {
        var html = "<h2>Hello <code unclosed</h2>"u8.ToArray();
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(1);
        var text = HeadingScanner.DecodeText(html, in headings[0]);
        await Assert.That(text).IsEqualTo("Hello");
    }

    /// <summary>Heading level 0 is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LevelZeroRejected() =>
        await Assert.That(HeadingScanner.Scan("<h0>x</h0>"u8.ToArray()).Length).IsEqualTo(0);

    /// <summary>A stray less-than that does not start a tag is skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StrayLessThan() =>
        await Assert.That(HeadingScanner.Scan("<<><<<h2>ok</h2>"u8.ToArray()).Length).IsEqualTo(1);
}
