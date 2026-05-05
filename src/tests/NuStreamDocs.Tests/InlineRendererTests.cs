// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end tests for the inline parser / renderer.</summary>
public class InlineRendererTests
{
    /// <summary>Plain text should render unchanged with HTML escaping.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EscapesRawText()
    {
        var html = Render("a < b & c");
        await Assert.That(html).IsEqualTo("a &lt; b &amp; c");
    }

    /// <summary>Inline code spans should produce a code element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersCodeSpan()
    {
        var html = Render("hello `world` end");
        await Assert.That(html).IsEqualTo("hello <code>world</code> end");
    }

    /// <summary>Strong + emphasis + nested code should all render.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersStrongEmAndNestedCode()
    {
        var html = Render("**bold** and *em with `c`*");
        await Assert.That(html).IsEqualTo("<strong>bold</strong> and <em>em with <code>c</code></em>");
    }

    /// <summary>Inline links should render as anchors with escaped href.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersInlineLink()
    {
        var html = Render("see [docs](https://example.test/?a=b&c=d)");
        await Assert.That(html).IsEqualTo("see <a href=\"https://example.test/?a=b&amp;c=d\">docs</a>");
    }

    /// <summary>Autolinks should expand to anchor elements with the URI as both href and text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersAutolink()
    {
        var html = Render("visit <https://example.test>");
        await Assert.That(html).IsEqualTo("visit <a href=\"https://example.test\">https://example.test</a>");
    }

    /// <summary>Inline raw HTML opening + closing tags pass through verbatim per CommonMark §6.6.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PassesThroughInlineRawHtml()
    {
        var html = Render("see <a href=\"x\">link</a> for more");
        await Assert.That(html).IsEqualTo("see <a href=\"x\">link</a> for more");
    }

    /// <summary>Inline raw HTML self-closing tags pass through verbatim.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PassesThroughInlineSelfClosingTag()
    {
        var html = Render("line<br/>break");
        await Assert.That(html).IsEqualTo("line<br/>break");
    }

    /// <summary>Inline HTML comments pass through verbatim.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PassesThroughInlineHtmlComment()
    {
        var html = Render("a <!-- hi --> b");
        await Assert.That(html).IsEqualTo("a <!-- hi --> b");
    }

    /// <summary>A bare <c>&lt;</c> that isn't followed by a tag-name byte stays escaped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EscapesBareLessThan()
    {
        var html = Render("a < b");
        await Assert.That(html).IsEqualTo("a &lt; b");
    }

    /// <summary>Two trailing spaces before a newline should produce a hard break.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersHardBreak()
    {
        var html = Render("line one  \nline two");
        await Assert.That(html).IsEqualTo("line one<br />\nline two");
    }

    /// <summary>Backslash escapes punctuation; the backslash itself is consumed.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BackslashEscapesPunctuation()
    {
        var html = Render(@"a \* literal");
        await Assert.That(html).IsEqualTo("a * literal");
    }

    /// <summary>Helper that runs the renderer against a UTF-8 string and returns the result.</summary>
    /// <param name="markdown">Inline markdown source.</param>
    /// <returns>The rendered HTML string.</returns>
    private static string Render(string markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        InlineRenderer.Render(Encoding.UTF8.GetBytes(markdown), writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
