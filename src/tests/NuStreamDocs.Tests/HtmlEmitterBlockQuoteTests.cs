// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Block quote rendering through the public <c>MarkdownRenderer</c> entry point.</summary>
public class HtmlEmitterBlockQuoteTests
{
    /// <summary>A single quoted line renders as a block quote holding one paragraph.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleLineRendersAsBlockQuote()
    {
        var html = Render("> quote"u8);
        await Assert.That(html).IsEqualTo("<blockquote>\n<p>quote</p>\n</blockquote>\n");
    }

    /// <summary>Consecutive quoted lines form one paragraph.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConsecutiveLinesFormOneParagraph()
    {
        var html = Render("> a\n> b"u8);
        await Assert.That(html).IsEqualTo("<blockquote>\n<p>a\nb</p>\n</blockquote>\n");
    }

    /// <summary>A marker-only line separates two quoted paragraphs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkerOnlyLineSeparatesParagraphs()
    {
        var html = Render("> a\n>\n> b"u8);
        await Assert.That(html).IsEqualTo("<blockquote>\n<p>a</p>\n<p>b</p>\n</blockquote>\n");
    }

    /// <summary>A non-blank line directly after quoted text continues the quoted paragraph.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LazyContinuationJoinsQuotedParagraph()
    {
        var html = Render("> a\nb"u8);
        await Assert.That(html).IsEqualTo("<blockquote>\n<p>a\nb</p>\n</blockquote>\n");
    }

    /// <summary>A blank line ends the quote.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankLineEndsBlockQuote()
    {
        var html = Render("> a\n\nb"u8);
        await Assert.That(html).IsEqualTo("<blockquote>\n<p>a</p>\n</blockquote>\n<p>b</p>\n");
    }

    /// <summary>A block quote directly after a paragraph starts a new block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockQuoteInterruptsParagraph()
    {
        var html = Render("text\n> q"u8);
        await Assert.That(html).IsEqualTo("<p>text</p>\n<blockquote>\n<p>q</p>\n</blockquote>\n");
    }

    /// <summary>A quote inside a quote renders as a nested block quote.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task QuotesNest()
    {
        var html = Render("> > inner"u8);
        await Assert.That(html).IsEqualTo("<blockquote>\n<blockquote>\n<p>inner</p>\n</blockquote>\n</blockquote>\n");
    }

    /// <summary>Headings, lists and fenced code inside a quote render as blocks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task QuoteHoldsOtherBlocks()
    {
        var html = Render("> # title\n> - a\n> - b\n>\n> ```\n> code\n> ```"u8);
        await Assert.That(html).IsEqualTo(
            "<blockquote>\n<h1>title</h1>\n<ul>\n<li>a</li>\n<li>b</li>\n</ul>\n<pre><code>code\n</code></pre>\n</blockquote>\n");
    }

    /// <summary>A quoted line that opens a block does not take a following unmarked line as lazy text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnmarkedLineAfterQuotedHeadingEndsQuote()
    {
        var html = Render("> # title\nplain"u8);
        await Assert.That(html).IsEqualTo("<blockquote>\n<h1>title</h1>\n</blockquote>\n<p>plain</p>\n");
    }

    /// <summary>A lone <c>=</c> line with no paragraph above is plain text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoneEqualsLineIsParagraphText()
    {
        var html = Render("=\ntext"u8);
        await Assert.That(html).IsEqualTo("<p>=\ntext</p>\n");
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
