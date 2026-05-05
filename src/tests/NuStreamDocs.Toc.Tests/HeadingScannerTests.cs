// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace NuStreamDocs.Toc.Tests;

/// <summary>Tests for <c>HeadingScanner</c>.</summary>
public class HeadingScannerTests
{
    /// <summary>Scanner finds h1/h2/h3 in order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FindsAllStandardHeadings()
    {
        byte[] html = [.. "<h1>One</h1><p>x</p><h2>Two</h2><h3>Three</h3>"u8];
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(3);
        await Assert.That(headings[0].Level).IsEqualTo(1);
        await Assert.That(headings[1].Level).IsEqualTo(2);
        await Assert.That(headings[2].Level).IsEqualTo(3);
    }

    /// <summary>Non-heading tags are ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsNonHeadingElements()
    {
        byte[] html = [.. "<header>x</header><hr><p>none</p><div>nope</div>"u8];
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(0);
    }

    /// <summary>Existing id attribute is captured as offset+length into the original snapshot.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CapturesExistingId()
    {
        byte[] html = [.. "<h2 id=\"intro\" class=\"x\">Hello</h2>"u8];
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(1);
        await Assert.That(headings[0].HasExistingId).IsTrue();
        await Assert.That(headings[0].ExistingIdBytes(html).SequenceEqual("intro"u8)).IsTrue();
    }

    /// <summary>Heading level out of range is ignored.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IgnoresOutOfRangeLevel()
    {
        byte[] html = [.. "<h7>too deep</h7><h2>ok</h2>"u8];
        var headings = HeadingScanner.Scan(html);
        await Assert.That(headings.Length).IsEqualTo(1);
        await Assert.That(headings[0].Level).IsEqualTo(2);
    }

    /// <summary>DecodeTextInto streams the stripped text bytes without UTF-16 transcoding.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DecodeTextIntoEmitsBytes()
    {
        byte[] html = [.. "<h2>Hello <code>World</code></h2>"u8];
        var headings = HeadingScanner.Scan(html);
        ArrayBufferWriter<byte> sink = new(32);
        HeadingScanner.DecodeTextInto(html, in headings[0], sink);
        await Assert.That(sink.WrittenSpan.SequenceEqual("Hello World"u8)).IsTrue();
    }
}
