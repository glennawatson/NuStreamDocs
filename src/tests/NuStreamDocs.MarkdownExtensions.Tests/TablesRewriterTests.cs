// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Tables;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behaviour tests for <c>TablesRewriter</c>.</summary>
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

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        TablesRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
