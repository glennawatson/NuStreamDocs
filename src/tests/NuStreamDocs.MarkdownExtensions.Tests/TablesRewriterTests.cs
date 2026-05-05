// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Tables;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>TablesRewriter</c>.</summary>
public class TablesRewriterTests
{
    /// <summary>A header + separator + body row produces a complete <c>&lt;table&gt;</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsCompleteTable()
    {
        var output = Rewrite("| h1 | h2 |\n| --- | --- |\n| a | b |\n");
        await Assert.That(output).Contains("<table>");
        await Assert.That(output).Contains("<thead>");
        await Assert.That(output).Contains("<th>h1</th><th>h2</th>");
        await Assert.That(output).Contains("<tbody>");
        await Assert.That(output).Contains("<td>a</td><td>b</td>");
    }

    /// <summary>Alignment markers in the separator produce <c>style="text-align:…"</c> on each cell.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task AppliesColumnAlignments()
    {
        var output = Rewrite("| L | R | C |\n| :--- | ---: | :---: |\n| a | b | c |\n");
        await Assert.That(output).Contains("<th style=\"text-align:left\">L</th>");
        await Assert.That(output).Contains("<th style=\"text-align:right\">R</th>");
        await Assert.That(output).Contains("<th style=\"text-align:center\">C</th>");
    }

    /// <summary>A pipe-line followed by a non-separator line is not parsed as a table.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SkipsWhenNoSeparator()
    {
        var output = Rewrite("| not | a | table |\nplain text\n");
        await Assert.That(output).DoesNotContain("<table>");
    }

    /// <summary>
    /// Inline markdown inside body cells renders to HTML — `[label](href)` becomes an anchor,
    /// `*emphasis*` becomes &lt;em&gt;, and `` `code` `` becomes &lt;code&gt;. Without this, the API
    /// reference tables that the C# generator emits show literal markdown link syntax instead of
    /// clickable links — the `[Type](Type.md)` cells stay as raw text.
    /// </summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BodyCellRendersInlineMarkdownLinksAndEmphasis()
    {
        var output = Rewrite(
            "| Type | Notes |\n" +
            "| --- | --- |\n" +
            "| [AkavacheBuilderExtensions](AkavacheBuilderExtensions.md) | use *carefully* with `cache.Get` |\n");

        await Assert.That(output).Contains("<a href=\"AkavacheBuilderExtensions.md\">AkavacheBuilderExtensions</a>");
        await Assert.That(output).Contains("<em>carefully</em>");
        await Assert.That(output).Contains("<code>cache.Get</code>");

        // The literal markdown syntax must not survive into the rendered output.
        await Assert.That(output).DoesNotContain("[AkavacheBuilderExtensions]");
    }

    /// <summary>Header cells render inline markdown the same way body cells do.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HeaderCellRendersInlineMarkdown()
    {
        var output = Rewrite(
            "| [Spec](spec.md) | Count |\n" +
            "| --- | --- |\n" +
            "| row | 1 |\n");

        await Assert.That(output).Contains("<th><a href=\"spec.md\">Spec</a></th>");
    }

    /// <summary>Cells without inline markdown still emit escaped HTML — no regression on plain text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PlainCellsStillEscapeHtmlSpecials()
    {
        var output = Rewrite(
            "| h1 | h2 |\n" +
            "| --- | --- |\n" +
            "| a < b | x & y |\n");

        await Assert.That(output).Contains("<td>a &lt; b</td>");
        await Assert.That(output).Contains("<td>x &amp; y</td>");
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        ArrayBufferWriter<byte> sink = new();
        TablesRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
