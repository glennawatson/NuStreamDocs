// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Details;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>DetailsRewriter</c>.</summary>
public class DetailsRewriterTests
{
    /// <summary>A collapsed <c>???</c> block becomes a closed-by-default details element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesCollapsedDetails()
    {
        var output = Rewrite("??? note\n    body line\n");
        await Assert.That(output).Contains("<details class=\"note\">");
        await Assert.That(output).Contains("<summary>Note</summary>");
        await Assert.That(output).Contains("body line");
    }

    /// <summary>A <c>???+</c> block opens by default via the HTML <c>open</c> attribute.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesOpenByDefaultDetails()
    {
        var output = Rewrite("???+ tip \"Try this\"\n    body\n");
        await Assert.That(output).Contains("<details class=\"tip\" open>");
        await Assert.That(output).Contains("<summary>Try this</summary>");
    }

    /// <summary>Source without any details opener passes through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PassesThroughNonDetailsText()
    {
        var output = Rewrite("Just a paragraph.\n");
        await Assert.That(output).IsEqualTo("Just a paragraph.\n");
    }

    /// <summary>An opener with no type token (just <c>??? </c> followed by EOL) is left verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OpenerWithoutTypePassesThrough()
    {
        const string Input = "??? \n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>An already-uppercase type is preserved without an extra shift.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UppercaseTypeIsLeftIntact()
    {
        var output = Rewrite("??? NOTE\n    body\n");
        await Assert.That(output).Contains("<summary>NOTE</summary>");
    }

    /// <summary>HTML-special bytes in the title are entity-escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TitleEscapesHtmlSpecialBytes()
    {
        var output = Rewrite("??? note \"a&b<c\"\n    body\n");
        await Assert.That(output).Contains("a&amp;b&lt;c");
    }

    /// <summary>An opener line followed by trailing chars after the title is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingCharsAfterTitleRejected()
    {
        const string Input = "??? note \"Hi\" extra trailing\n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>An opener with a title that doesn't have a closing quote is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedTitleRejected()
    {
        const string Input = "??? note \"unclosed\n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        DetailsRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
