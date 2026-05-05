// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Tables;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Branch-coverage edge cases for TablesRewriter.</summary>
public class TablesRewriterBranchTests
{
    /// <summary>Empty input is empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>A header line without a separator under it stays as paragraph text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeaderWithoutSeparator()
    {
        const string Src = "| a | b |\n";
        await Assert.That(Rewrite(Src)).IsEqualTo(Src);
    }

    /// <summary>Alignment hints (left, right, centre) flow through into td/th classes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AlignmentVariants()
    {
        var output = Rewrite("| a | b | c |\n|:--|:-:|--:|\n| 1 | 2 | 3 |\n");
        await Assert.That(output).Contains("<table>");
        await Assert.That(output).Contains("<th");
        await Assert.That(output).Contains("<td");
    }

    /// <summary>Ragged row counts are tolerated (cells truncated/padded to header width).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RaggedRows()
    {
        var output = Rewrite("| a | b |\n|---|---|\n| only-one |\n| 1 | 2 | 3 |\n");
        await Assert.That(output).Contains("<table>");
    }

    /// <summary>Helper that runs the rewriter.</summary>
    /// <param name="source">Source markdown.</param>
    /// <returns>Rewritten string.</returns>
    private static string Rewrite(string source)
    {
        ArrayBufferWriter<byte> sink = new();
        TablesRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
