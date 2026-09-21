// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Heading rendering through the public <c>MarkdownRenderer</c> entry point.</summary>
public class HtmlEmitterHeadingTests
{
    /// <summary>A paragraph line over an <c>=</c> run renders as a level-1 heading.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EqualsUnderlineRendersLevelOneHeading()
    {
        var html = Render("Title\n====="u8);
        await Assert.That(html).IsEqualTo("<h1>Title</h1>\n");
    }

    /// <summary>A paragraph line over a <c>-</c> run renders as a level-2 heading.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HyphenUnderlineRendersLevelTwoHeading()
    {
        var html = Render("Sub\n---"u8);
        await Assert.That(html).IsEqualTo("<h2>Sub</h2>\n");
    }

    /// <summary>A single hyphen is a valid underline.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleHyphenUnderlineRendersHeading()
    {
        var html = Render("Sub\n-"u8);
        await Assert.That(html).IsEqualTo("<h2>Sub</h2>\n");
    }

    /// <summary>A multi-line paragraph above the underline becomes one heading.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultiLineTextBecomesOneHeading()
    {
        var html = Render("a\nb\n==="u8);
        await Assert.That(html).IsEqualTo("<h1>a\nb</h1>\n");
    }

    /// <summary>Trailing spaces after the underline and heading text are dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingSpacesAreDropped()
    {
        var html = Render("Title  \n===  "u8);
        await Assert.That(html).IsEqualTo("<h1>Title</h1>\n");
    }

    /// <summary>Inline markup inside setext heading text renders.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineMarkupRendersInsideHeading()
    {
        var html = Render("*Title*\n==="u8);
        await Assert.That(html).IsEqualTo("<h1><em>Title</em></h1>\n");
    }

    /// <summary>A blank line between the text and a hyphen run leaves a paragraph and a thematic break.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankLineBeforeHyphensIsThematicBreak()
    {
        var html = Render("text\n\n---"u8);
        await Assert.That(html).IsEqualTo("<p>text</p>\n<hr />\n");
    }

    /// <summary>A spaced hyphen run under a paragraph is a thematic break, not an underline.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpacedHyphensAreThematicBreak()
    {
        var html = Render("Foo\n- - -"u8);
        await Assert.That(html).IsEqualTo("<p>Foo</p>\n<hr />\n");
    }

    /// <summary>An asterisk run under a paragraph is a thematic break.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AsteriskRunUnderParagraphIsThematicBreak()
    {
        var html = Render("Foo\n***"u8);
        await Assert.That(html).IsEqualTo("<p>Foo</p>\n<hr />\n");
    }

    /// <summary>An equals run with an interior space is paragraph text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpacedEqualsAreParagraphText()
    {
        var html = Render("Foo\n= ="u8);
        await Assert.That(html).IsEqualTo("<p>Foo\n= =</p>\n");
    }

    /// <summary>A setext heading inside a list item renders as a heading child.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SetextHeadingInsideListItem()
    {
        var html = Render("- Title\n  ==="u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>\n<h1>Title</h1>\n</li>\n</ul>\n");
    }

    /// <summary>ATX headings drop trailing spaces and a closing hash run that follows whitespace.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected rendered HTML.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("# Title #", "<h1>Title</h1>\n")]
    [Arguments("## Title ##  ", "<h2>Title</h2>\n")]
    [Arguments("# Title   ", "<h1>Title</h1>\n")]
    [Arguments("### #", "<h3></h3>\n")]
    [Arguments("# foo#", "<h1>foo#</h1>\n")]
    [Arguments("# C#", "<h1>C#</h1>\n")]
    [Arguments("# Title # x", "<h1>Title # x</h1>\n")]
    [Arguments("#", "<h1></h1>\n")]
    public async Task AtxClosingSequenceIsStripped(string markdown, string expected) =>
        await Assert.That(Render(Encoding.UTF8.GetBytes(markdown))).IsEqualTo(expected);

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
