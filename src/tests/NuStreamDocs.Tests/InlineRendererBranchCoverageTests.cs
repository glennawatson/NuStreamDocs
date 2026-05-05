// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Edge-case inputs to widen branch coverage on InlineEscape, AutoLink, and RawHtml.</summary>
public class InlineRendererBranchCoverageTests
{
    /// <summary>Backslash at end of input is rendered literally.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BackslashAtEnd()
    {
        var html = Render(@"end\");
        await Assert.That(html).Contains("\\");
    }

    /// <summary>Backslash followed by a non-punctuation char is rendered literally.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BackslashNonPunct()
    {
        var html = Render("a\\b");
        await Assert.That(html).IsEqualTo("a\\b");
    }

    /// <summary>Unclosed angle bracket leaves the text literal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedAngleBracket()
    {
        var html = Render("see <https://no-close");
        await Assert.That(html).Contains("https://no-close");
    }

    /// <summary>Less-than followed by digit is escaped, not parsed as a tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LessThanDigit()
    {
        var html = Render("count is <5 here");
        await Assert.That(html).Contains("&lt;5");
    }

    /// <summary>Tag that lacks a closing > byte falls through to escaped text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TagWithoutCloser()
    {
        var html = Render("<div ");
        await Assert.That(html).Contains("&lt;");
    }

    /// <summary>Comment without proper close stays escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedComment()
    {
        var html = Render("a <!-- never ends");
        await Assert.That(html).Contains("&lt;!");
    }

    /// <summary>Empty inline code span produces empty code element.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyCodeSpan()
    {
        var html = Render("`` `` end");
        await Assert.That(html).IsNotNull();
    }

    /// <summary>Multiple emphasis runs nested.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedEmphasis()
    {
        var html = Render("***triple***");
        await Assert.That(html).Contains("<em>");
        await Assert.That(html).Contains("<strong>");
    }

    /// <summary>Underscore emphasis.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnderscoreEmphasis()
    {
        var html = Render("_em_");
        await Assert.That(html).IsEqualTo("<em>em</em>");
    }

    /// <summary>Helper that renders inline markdown to HTML.</summary>
    /// <param name="markdown">Inline source.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(string markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        InlineRenderer.Render(Encoding.UTF8.GetBytes(markdown), writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
