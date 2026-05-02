// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>
/// Smoke tests for the public <c>MarkdownRenderer</c> entry point.
/// </summary>
public class MarkdownRendererTests
{
    /// <summary>An ATX heading should render as the matching <c>&lt;hN&gt;</c> element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersAtxHeading()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("# Hello"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<h1>");
        await Assert.That(html).Contains("Hello");
    }

    /// <summary>Plain text should render as a <c>&lt;p&gt;</c> with HTML entities escaped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EscapesParagraphContent()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("a < b & c"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("&lt;");
        await Assert.That(html).Contains("&amp;");
    }

    /// <summary>Leading YAML frontmatter is stripped before block-scanning so the <c>---</c> opener and key-value lines never reach the rendered article body.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task StripsLeadingFrontmatter()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("---\ntitle: Home\nhide:\n  - navigation\n  - toc\n---\n\n# Hello"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<h1>");
        await Assert.That(html).Contains("Hello");
        await Assert.That(html).DoesNotContain("title: Home");
        await Assert.That(html).DoesNotContain("---");
    }

    /// <summary>A <c>&lt;div&gt;</c> opener (CommonMark Type 6 HTML block) is emitted verbatim instead of being paragraph-wrapped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsType6HtmlBlockVerbatim()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("<div class=\"admonition info\">\n<p>inner</p>\n</div>"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<div class=\"admonition info\">");
        await Assert.That(html).DoesNotContain("<p><div");
        await Assert.That(html).DoesNotContain("&lt;div");
    }

    /// <summary>A <c>&lt;table&gt;</c> opener is recognized as a Type 6 HTML block.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsTableHtmlBlockVerbatim()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("<table>\n<tr><td>cell</td></tr>\n</table>"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<table>");
        await Assert.That(html).Contains("<td>cell</td>");
        await Assert.That(html).DoesNotContain("<p><table");
    }

    /// <summary>A <c>&lt;details&gt;&lt;summary&gt;</c> block is emitted verbatim.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsDetailsHtmlBlockVerbatim()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("<details>\n<summary>Click</summary>\nhidden\n</details>"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<details>");
        await Assert.That(html).Contains("<summary>Click</summary>");
        await Assert.That(html).DoesNotContain("<p><details");
    }

    /// <summary>A <c>&lt;pre&gt;</c> Type 1 block continues until the matching <c>&lt;/pre&gt;</c> regardless of intervening blank lines.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsType1PreBlockUntilCloseTag()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("<pre>\nfirst\n\nsecond\n</pre>"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<pre>");
        await Assert.That(html).Contains("first");
        await Assert.That(html).Contains("second");
        await Assert.That(html).Contains("</pre>");
        await Assert.That(html).DoesNotContain("<p>second");
    }

    /// <summary>A Type 6 HTML block ends at the first blank line; markdown after the blank line resumes normal rendering.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Type6BlockEndsAtBlankLine()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("<div>raw</div>\n\n# After"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<div>raw</div>");
        await Assert.That(html).Contains("<h1>");
        await Assert.That(html).Contains("After");
    }

    /// <summary>A leading <c>&lt;</c> with a non-tag-name byte right after (like <c>&lt; b</c>) falls through to paragraph + entity escape — does not open an HTML block.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BareAngleBracketDoesNotOpenHtmlBlock()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("< not-a-tag"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<p>");
        await Assert.That(html).Contains("&lt;");
    }
}
