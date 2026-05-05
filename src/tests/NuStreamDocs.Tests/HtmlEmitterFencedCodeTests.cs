// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Html;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>End-to-end tests exercising HtmlEmitter fenced-code paths.</summary>
public class HtmlEmitterFencedCodeTests
{
    /// <summary>Renders a fenced code block with a language tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeWithLanguage()
    {
        var html = Render("```cs\nvar x = 1;\n```\n");
        await Assert.That(html).Contains("<pre><code class=\"language-cs\">");
        await Assert.That(html).Contains("var x = 1;");
        await Assert.That(html).Contains("</code></pre>");
    }

    /// <summary>Renders a fenced code block with no language.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeNoLanguage()
    {
        var html = Render("```\nplain\n```\n");
        await Assert.That(html).Contains("<pre><code>");
        await Assert.That(html).Contains("plain");
    }

    /// <summary>Tilde fences also produce a code block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TildeFences()
    {
        var html = Render("~~~py\nprint(1)\n~~~\n");
        await Assert.That(html).Contains("language-py");
    }

    /// <summary>Code body characters needing escape are escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EscapesCodeBody()
    {
        var html = Render("```\n<div>\n```\n");
        await Assert.That(html).Contains("&lt;div&gt;");
    }

    /// <summary>Headings of various levels render with the right tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeadingLevels()
    {
        var html = Render("# h1\n## h2\n### h3\n#### h4\n##### h5\n###### h6\n");
        for (var i = 1; i <= 6; i++)
        {
            await Assert.That(html).Contains($"<h{i}>");
        }
    }

    /// <summary>Helper that runs the full block→inline→HTML pipeline.</summary>
    /// <param name="markdown">UTF-8 markdown source.</param>
    /// <returns>Rendered HTML string.</returns>
    private static string Render(string markdown)
    {
        var bytes = Encoding.UTF8.GetBytes(markdown);
        ArrayBufferWriter<BlockSpan> blockSink = new();
        BlockScanner.Scan(bytes, blockSink);
        ArrayBufferWriter<byte> htmlSink = new();
        HtmlEmitter.Emit(bytes, blockSink.WrittenSpan, htmlSink);
        return Encoding.UTF8.GetString(htmlSink.WrittenSpan);
    }
}
