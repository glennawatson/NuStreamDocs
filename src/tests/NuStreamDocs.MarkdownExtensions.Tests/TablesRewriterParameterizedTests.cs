// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Tables;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Parameterized alignment / row-shape coverage for TablesRewriter.</summary>
public class TablesRewriterParameterizedTests
{
    /// <summary>Each alignment marker on the separator row produces the expected style alignment.</summary>
    /// <param name="separator">Separator row.</param>
    /// <param name="expectedAlign">Expected text-align fragment.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("|:---|", "left")]
    [Arguments("|:---:|", "center")]
    [Arguments("|---:|", "right")]
    [Arguments("|---|", "")]
    public async Task ColumnAlignment(string separator, string expectedAlign)
    {
        ArgumentNullException.ThrowIfNull(expectedAlign);
        var output = Rewrite($"| h |\n{separator}\n| v |\n");
        await Assert.That(output).Contains("<table>");
        await Assert.That(expectedAlign.Length is 0 || output.Contains(expectedAlign, StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Tables with varying column counts emit the matching number of cells in the header.</summary>
    /// <param name="header">Header row.</param>
    /// <param name="separator">Separator row.</param>
    /// <param name="expectedTh">Expected number of <c>&lt;th</c> elements.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("| a |", "|---|", 1)]
    [Arguments("| a | b |", "|---|---|", 2)]
    [Arguments("| a | b | c |", "|---|---|---|", 3)]
    [Arguments("| a | b | c | d |", "|---|---|---|---|", 4)]
    public async Task ColumnCountMatchesHeader(string header, string separator, int expectedTh)
    {
        var output = Rewrite($"{header}\n{separator}\n| v |\n");
        var thOpens = output.Split("<th>").Length - 1 + (output.Split("<th ").Length - 1);
        await Assert.That(thOpens).IsEqualTo(expectedTh);
    }

    /// <summary>Empty input or non-table source passes through unchanged.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("just a paragraph\n")]
    [Arguments("# heading\n")]
    [Arguments("| header only |\n")]
    public async Task NonTableSourcePassThrough(string source) =>
        await Assert.That(Rewrite(source)).IsEqualTo(source);

    /// <summary>Helper that runs the rewriter.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Rewritten string.</returns>
    private static string Rewrite(string source)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        TablesRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
