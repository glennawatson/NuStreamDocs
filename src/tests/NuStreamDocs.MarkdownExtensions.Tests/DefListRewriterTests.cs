// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.DefList;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>DefListRewriter</c>.</summary>
public class DefListRewriterTests
{
    /// <summary>A term followed by a definition becomes a <c>&lt;dl&gt;</c> with one <c>&lt;dt&gt;</c>/<c>&lt;dd&gt;</c> pair.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesSinglePair()
    {
        var output = Rewrite("Term\n: defn\n");
        await Assert.That(output).Contains("<dl>");
        await Assert.That(output).Contains("<dt>Term</dt>");
        await Assert.That(output).Contains("<dd>defn</dd>");
    }

    /// <summary>Multiple definitions for one term emit multiple <c>&lt;dd&gt;</c> elements.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesMultipleDefinitions()
    {
        var output = Rewrite("Term\n: first\n: second\n");
        await Assert.That(output).Contains("<dd>first</dd>");
        await Assert.That(output).Contains("<dd>second</dd>");
    }

    /// <summary>Plain prose without any <c>: </c> definition lines is left alone.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LeavesPlainProseAlone()
    {
        var output = Rewrite("Just a paragraph.\nWith two lines.\n");
        await Assert.That(output).DoesNotContain("<dl>");
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        ArrayBufferWriter<byte> sink = new();
        DefListRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
