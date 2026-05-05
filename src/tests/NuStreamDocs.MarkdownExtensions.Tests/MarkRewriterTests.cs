// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Mark;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>MarkRewriter</c>.</summary>
public class MarkRewriterTests
{
    /// <summary>A matched <c>==text==</c> span becomes a <c>&lt;mark&gt;</c> element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesMatchedSpan()
    {
        var output = Rewrite("hello ==world== there");
        await Assert.That(output).IsEqualTo("hello <mark>world</mark> there");
    }

    /// <summary>Mark tokens inside a fenced-code block are left untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IgnoresFencedCodeBlocks()
    {
        var output = Rewrite("```\nx ==y== z\n```\n");
        await Assert.That(output).Contains("x ==y== z");
        await Assert.That(output).DoesNotContain("<mark>");
    }

    /// <summary>Mark tokens inside an inline-code span are left untouched.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IgnoresInlineCodeSpans()
    {
        var output = Rewrite("call `foo ==bar== baz` ok");
        await Assert.That(output).DoesNotContain("<mark>");
    }

    /// <summary>An unmatched <c>==</c> opener is left as plain text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LeavesUnmatchedOpenerAlone()
    {
        var output = Rewrite("just == text without a closer");
        await Assert.That(output).IsEqualTo("just == text without a closer");
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        ArrayBufferWriter<byte> sink = new();
        MarkRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
