// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.MdInHtml;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>End-to-end coverage for the mkdocs-material grid-cards pattern: <c>&lt;div class="grid cards" markdown&gt;</c> + dashed list.</summary>
public class MdInHtmlGridCardsTests
{
    /// <summary>The grid-cards container's body should parse as a real list, not be wrapped per line in <c>&lt;p&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GridCardsContainerEmitsList()
    {
        const string Source = """
            <div class="grid cards" markdown>

            -   item one

                ---

                first body

            -   item two

                ---

                second body

            </div>
            """;
        var rewritten = Rewrite(Source);
        var html = Render(rewritten);

        // The rewritten source should still strip the markdown attribute.
        await Assert.That(rewritten).Contains("<div class=\"grid cards\">");
        await Assert.That(rewritten).DoesNotContain("markdown>");

        // The renderer should produce a loose list — each item's body becomes paragraphs +
        // an interior <hr/> from the indented `---`.
        await Assert.That(html).Contains("<ul");
        await Assert.That(html).Contains("<p>item one");
        await Assert.That(html).Contains("<p>item two");
        await Assert.That(html).Contains("<hr />");
        await Assert.That(html).Contains("<p>first body");
        await Assert.That(html).Contains("<p>second body");
        await Assert.That(html).DoesNotContain("<p>-   item");
    }

    /// <summary>A bare top-level <c>---</c> is recognized as a thematic break and renders as <c>&lt;hr /&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TopLevelThematicBreakRendersHr()
    {
        const string Source = "before\n\n---\n\nafter\n";
        var html = Render(Source);
        await Assert.That(html).Contains("<hr />");
        await Assert.That(html).Contains("<p>before");
        await Assert.That(html).Contains("<p>after");
    }

    /// <summary>Runs the md_in_html preprocessor and returns the result as a string.</summary>
    /// <param name="source">Markdown source.</param>
    /// <returns>Rewritten source.</returns>
    private static string Rewrite(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        MdInHtmlRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }

    /// <summary>Runs the markdown renderer and returns the HTML.</summary>
    /// <param name="source">Markdown source.</param>
    /// <returns>HTML string.</returns>
    private static string Render(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
