// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Paragraph rendering through the public <c>MarkdownRenderer</c> entry point.</summary>
public class HtmlEmitterParagraphTests
{
    /// <summary>Lines of one paragraph are joined by newlines.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinesJoinWithNewline()
    {
        var html = Render("one\ntwo"u8);
        await Assert.That(html).IsEqualTo("<p>one\ntwo</p>\n");
    }

    /// <summary>Two trailing spaces before a newline produce a hard break.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoTrailingSpacesMakeHardBreak()
    {
        var html = Render("one  \ntwo"u8);
        await Assert.That(html).IsEqualTo("<p>one<br />\ntwo</p>\n");
    }

    /// <summary>A single trailing space is not a hard break.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OneTrailingSpaceIsNotHardBreak()
    {
        var html = Render("one \ntwo"u8);
        await Assert.That(html).IsEqualTo("<p>one \ntwo</p>\n");
    }

    /// <summary>A backslash before a newline stays literal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingBackslashStaysLiteral()
    {
        var html = Render("one\\\ntwo"u8);
        await Assert.That(html).IsEqualTo("<p>one\\\ntwo</p>\n");
    }

    /// <summary>Trailing spaces on the last line of a paragraph are dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingSpacesOnLastLineAreDropped()
    {
        var html = Render("one  "u8);
        await Assert.That(html).IsEqualTo("<p>one</p>\n");
    }

    /// <summary>Link text that wraps onto a second line still forms a link.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkTextWrapsAcrossLines()
    {
        var html = Render("see [long\ntext](/u) now"u8);
        await Assert.That(html).IsEqualTo("<p>see <a href=\"/u\">long\ntext</a> now</p>\n");
    }

    /// <summary>Emphasis that wraps onto a second line still forms emphasis.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmphasisWrapsAcrossLines()
    {
        var html = Render("a *b\nc* d"u8);
        await Assert.That(html).IsEqualTo("<p>a <em>b\nc</em> d</p>\n");
    }

    /// <summary>A code span that wraps onto a second line still forms a code span.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CodeSpanWrapsAcrossLines()
    {
        var html = Render("a `b\nc` d"u8);
        await Assert.That(html).IsEqualTo("<p>a <code>b\nc</code> d</p>\n");
    }

    /// <summary>Wrapped link text inside a list item still forms a link.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LinkTextWrapsInsideListItem()
    {
        var html = Render("- see [long\n  text](/u)"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>see <a href=\"/u\">long\ntext</a></li>\n</ul>\n");
    }

    /// <summary>Renders <paramref name="markdown"/> to an HTML string.</summary>
    /// <param name="markdown">UTF-8 markdown.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(ReadOnlySpan<byte> markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render(markdown, writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
