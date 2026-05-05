// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tags.Tests;

/// <summary>Direct tests for the shared Utf8HtmlScanner helpers — heading-open detection and attribute-value extraction.</summary>
public class Utf8HtmlScannerTests
{
    /// <summary>Locates h1..h6 open tags and reports the level + tag span.</summary>
    /// <param name="level">Heading level digit.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task FindHeadingForEachLevel(int level)
    {
        var html = Encoding.UTF8.GetBytes($"prefix<h{level}>x</h{level}>");
        var found = Utf8HtmlScanner.TryFindNextHeadingOpen(html, 0, out var tagStart, out var tagEnd, out var detectedLevel);
        await Assert.That(found).IsTrue();
        await Assert.That(detectedLevel).IsEqualTo(level);
        await Assert.That(tagStart).IsEqualTo("prefix"u8.Length);
        await Assert.That(tagEnd).IsEqualTo("prefix"u8.Length + 4);
    }

    /// <summary>Tags with attributes are bounded correctly at the closing angle.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TagWithAttributesBoundsToCloseAngle()
    {
        byte[] html = [.. "<h2 id=\"intro\" class=\"x\">body</h2>"u8];
        var found = Utf8HtmlScanner.TryFindNextHeadingOpen(html, 0, out var tagStart, out var tagEnd, out var level);
        await Assert.That(found).IsTrue();
        await Assert.That(level).IsEqualTo(2);
        await Assert.That(tagStart).IsEqualTo(0);
        await Assert.That(html[tagEnd - 1]).IsEqualTo((byte)'>');
    }

    /// <summary>Out-of-range and look-alike tags are rejected.</summary>
    /// <param name="html">Test bytes.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("<h0>x</h0>")]
    [Arguments("<h7>x</h7>")]
    [Arguments("<header>x</header>")]
    [Arguments("<hgroup>x</hgroup>")]
    [Arguments("<p>plain</p>")]
    public async Task NonHeadingsRejected(string html)
    {
        var bytes = Encoding.UTF8.GetBytes(html);
        await Assert.That(Utf8HtmlScanner.TryFindNextHeadingOpen(bytes, 0, out _, out _, out _)).IsFalse();
    }

    /// <summary>Attribute extraction handles double-quoted values.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AttributeDoubleQuoted()
    {
        var openTag = "<h2 id=\"intro\" class=\"x\">"u8;
        var (start, length) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
        await Assert.That(openTag.Slice(start, length).SequenceEqual("intro"u8)).IsTrue();
    }

    /// <summary>Attribute extraction handles single-quoted values.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AttributeSingleQuoted()
    {
        var openTag = "<h2 id='intro'>"u8;
        var (start, length) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
        await Assert.That(openTag.Slice(start, length).SequenceEqual("intro"u8)).IsTrue();
    }

    /// <summary>Attribute extraction handles unquoted values up to whitespace or close.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AttributeUnquoted()
    {
        var openTag = "<h2 id=plain class=\"x\">"u8;
        var (start, length) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
        await Assert.That(openTag.Slice(start, length).SequenceEqual("plain"u8)).IsTrue();
    }

    /// <summary>Attribute name match is exact — partial / suffix names do not match.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AttributeNameExactMatch()
    {
        var openTag = "<a data-id=\"x\" id=\"correct\">"u8;
        var (start, length) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
        await Assert.That(openTag.Slice(start, length).SequenceEqual("correct"u8)).IsTrue();
    }

    /// <summary>Missing attribute returns the sentinel.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AttributeMissing()
    {
        var openTag = "<h2 class=\"x\">"u8;
        var (start, length) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
        await Assert.That(start).IsEqualTo(-1);
        await Assert.That(length).IsEqualTo(0);
    }

    /// <summary>Quoted attribute without a closing quote is reported as missing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AttributeUnclosedQuoteMissing()
    {
        var openTag = "<h2 id=\"never"u8;
        var (start, length) = Utf8HtmlScanner.FindAttributeValue(openTag, "id"u8);
        await Assert.That(start).IsEqualTo(-1);
        await Assert.That(length).IsEqualTo(0);
    }

    /// <summary>Sequential heading scan tracks the cursor between matches.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FindNextHeadingAdvancesPastPrevious()
    {
        byte[] html = [.. "<h1>A</h1><p>x</p><h3>B</h3>"u8];
        Utf8HtmlScanner.TryFindNextHeadingOpen(html, 0, out _, out var tagEnd, out var level1);
        await Assert.That(level1).IsEqualTo(1);

        Utf8HtmlScanner.TryFindNextHeadingOpen(html, tagEnd, out _, out _, out var level2);
        await Assert.That(level2).IsEqualTo(3);
    }
}
