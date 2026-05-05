// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

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
        await Assert.That(HeadingScanner.Scan([.. "<h2>no close"u8]).Length).IsEqualTo(0);

    /// <summary>Single-quoted id attribute is captured.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleQuotedId()
    {
        byte[] html = [.. "<h2 id='single'>x</h2>"u8];
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings[0].ExistingIdBytes(html).SequenceEqual("single"u8)).IsTrue();
    }

    /// <summary>Quoted id without a closing quote yields an empty existing id.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedQuoteId()
    {
        byte[] html = [.. "<h2 id=\"never\">body</h2>"u8];
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(1);
        await Assert.That(headings[0].ExistingIdBytes(html).SequenceEqual("never"u8)).IsTrue();
    }

    /// <summary>Unquoted id attribute is captured up to whitespace or close.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnquotedId()
    {
        byte[] html = [.. "<h2 id=plain class=x>body</h2>"u8];
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(1);
        await Assert.That(headings[0].ExistingIdBytes(html).SequenceEqual("plain"u8)).IsTrue();
    }

    /// <summary>DecodeTextInto with body containing an unclosed inner tag emits the prefix.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DecodeUnclosedInnerTag()
    {
        byte[] html = [.. "<h2>Hello <code unclosed</h2>"u8];
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(1);
        ArrayBufferWriter<byte> sink = new(16);
        HeadingScanner.DecodeTextInto(html, in headings[0], sink);
        await Assert.That(sink.WrittenSpan.SequenceEqual("Hello "u8)).IsTrue();
    }

    /// <summary>Heading level 0 is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LevelZeroRejected() =>
        await Assert.That(HeadingScanner.Scan([.. "<h0>x</h0>"u8]).Length).IsEqualTo(0);

    /// <summary>A stray less-than that does not start a tag is skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StrayLessThan() =>
        await Assert.That(HeadingScanner.Scan([.. "<<><<<h2>ok</h2>"u8]).Length).IsEqualTo(1);
}
