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
        ArrayBufferWriter<byte> writer = new();
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
        ArrayBufferWriter<byte> writer = new();
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
        ArrayBufferWriter<byte> writer = new();
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
        ArrayBufferWriter<byte> writer = new();
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
        ArrayBufferWriter<byte> writer = new();
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
        ArrayBufferWriter<byte> writer = new();
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
        ArrayBufferWriter<byte> writer = new();
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
        ArrayBufferWriter<byte> writer = new();
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
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render("< not-a-tag"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<p>");
        await Assert.That(html).Contains("&lt;");
    }

    /// <summary>
    /// Full reference-style links (<c>[text][label]</c> with a trailing <c>[label]: url</c> definition)
    /// resolve to <c>&lt;a&gt;</c> elements and the definition line is stripped from the output.
    /// </summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolvesFullReferenceStyleLinks()
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render("See [the docs][docs] for details.\n\n[docs]: https://example.com/docs"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<a href=\"https://example.com/docs\">");
        await Assert.That(html).Contains(">the docs</a>");
        await Assert.That(html).DoesNotContain("[docs]:");
    }

    /// <summary>Collapsed reference-style links (<c>[label][]</c>) reuse the visible label as the lookup key.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolvesCollapsedReferenceStyleLinks()
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render("Try [search][] later.\n\n[search]: /search/"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<a href=\"/search/\">");
        await Assert.That(html).Contains(">search</a>");
    }

    /// <summary>Shortcut reference-style links (<c>[label]</c> with no second bracket pair) resolve when the label maps to a definition.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolvesShortcutReferenceStyleLinks()
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render("Read the [intro] first.\n\n[intro]: /guide/intro/"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<a href=\"/guide/intro/\">");
        await Assert.That(html).Contains(">intro</a>");
    }

    /// <summary>Reference-link labels match case-insensitively and treat internal whitespace runs as a single space.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReferenceLabelsMatchCaseInsensitively()
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render("See [Docs][CORE doc].\n\n[core  doc]: index.md"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<a href=\"index.md\">");
        await Assert.That(html).Contains(">Docs</a>");
    }

    /// <summary>4-space indented code blocks render as <c>&lt;pre&gt;&lt;code&gt;</c> with the indent stripped; the previous behaviour leaked them as paragraphs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndentedCodeRendersAsPreCodeBlock()
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render("Header line.\n\n    Install-Package ReactiveUI.WPF\n\nTrailing prose."u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<pre><code>Install-Package ReactiveUI.WPF\n</code></pre>");
        await Assert.That(html).DoesNotContain("<p>    Install-Package");
    }

    /// <summary>Multi-line indented blocks coalesce into a single <c>&lt;pre&gt;&lt;code&gt;</c>, with internal blank lines preserved as empty body lines (CommonMark §4.4).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IndentedCodePreservesInternalBlankLines()
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render(":\n\n    line one\n\n    line three\n\nafter."u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<pre><code>line one\n\nline three\n</code></pre>");
    }

    /// <summary>Definition titles are accepted by the parser; the href still resolves cleanly even when a title is present (titles are dropped pending native <c>LinkSpan</c> support).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReferenceDefinitionsAcceptTitles()
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render("Visit [home][h].\n\n[h]: /home \"Home page\""u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("href=\"/home\"");
        await Assert.That(html).Contains(">home</a>");
    }
}
