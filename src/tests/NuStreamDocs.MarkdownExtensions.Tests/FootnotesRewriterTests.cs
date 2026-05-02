// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Footnotes;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>FootnotesRewriter</c>.</summary>
public class FootnotesRewriterTests
{
    /// <summary>An inline reference is rewritten to a numbered superscript link.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesInlineReferenceToSuperscriptLink()
    {
        var output = Rewrite("Body[^1].\n\n[^1]: definition\n");
        await Assert.That(output).Contains("<sup id=\"fnref-1\"><a href=\"#fn-1\">1</a></sup>");
    }

    /// <summary>The collected definitions appear at the end inside a footnotes section.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsTrailingFootnotesSection()
    {
        var output = Rewrite("See[^x].\n\n[^x]: long-form note\n");
        await Assert.That(output).Contains("<section class=\"footnotes\">");
        await Assert.That(output).Contains("<li id=\"fn-x\">long-form note");
        await Assert.That(output).Contains("href=\"#fnref-x\"");
    }

    /// <summary>Footnote bodies render inline markdown (bold, code) instead of being escaped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersInlineMarkdownInBody()
    {
        var output = Rewrite("Body[^1].\n\n[^1]: with **bold** and `code`\n");
        await Assert.That(output).Contains("<strong>bold</strong>");
        await Assert.That(output).Contains("<code>code</code>");
    }

    /// <summary>Source without any footnote tokens passes through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PassesThroughWhenNoFootnotes()
    {
        var output = Rewrite("# Just a heading\n\nNothing to see here.\n");
        await Assert.That(output).IsEqualTo("# Just a heading\n\nNothing to see here.\n");
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        FootnotesRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
