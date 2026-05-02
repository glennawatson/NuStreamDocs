// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Admonitions;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>AdmonitionRewriter</c>.</summary>
public class AdmonitionRewriterTests
{
    /// <summary>An untitled <c>!!! note</c> block becomes a div with a title-cased default summary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RewritesUntitledNote()
    {
        var output = Rewrite("!!! note\n    body line\n");
        await Assert.That(output).Contains("<div class=\"admonition note\">");
        await Assert.That(output).Contains("<p class=\"admonition-title\">Note</p>");
        await Assert.That(output).Contains("body line");
    }

    /// <summary>A titled admonition uses the supplied label instead of the type token.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UsesProvidedTitle()
    {
        var output = Rewrite("!!! warning \"Heads up\"\n    careful\n");
        await Assert.That(output).Contains("<div class=\"admonition warning\">");
        await Assert.That(output).Contains("<p class=\"admonition-title\">Heads up</p>");
    }

    /// <summary>Body lines are de-indented by four spaces (or one tab).</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DeindentsBody()
    {
        var output = Rewrite("!!! tip\n    line one\n    line two\n");
        await Assert.That(output).Contains("line one\nline two");
    }

    /// <summary>Surrounding non-admonition text passes through unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PassesThroughNonAdmonitionText()
    {
        var output = Rewrite("# Heading\n\nA paragraph.\n");
        await Assert.That(output).IsEqualTo("# Heading\n\nA paragraph.\n");
    }

    /// <summary>An opener with no type token is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OpenerWithoutTypeRejected()
    {
        const string Input = "!!! \n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>No title falls back to title-cased type.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoTitleFallsBackToTitleCasedType()
    {
        var output = Rewrite("!!! warning\n    body\n");
        await Assert.That(output).Contains(">Warning</p>");
    }

    /// <summary>An already-uppercase type is left intact.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UppercaseTypeIsLeftIntact()
    {
        var output = Rewrite("!!! NOTE\n    body\n");
        await Assert.That(output).Contains(">NOTE</p>");
    }

    /// <summary>HTML-special bytes in the title are entity-escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TitleEscapesHtmlSpecialBytes()
    {
        var output = Rewrite("!!! note \"a&b<c\"\n    body\n");
        await Assert.That(output).Contains("a&amp;b&lt;c");
    }

    /// <summary>Trailing characters after the title are rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingCharsAfterTitleRejected()
    {
        const string Input = "!!! note \"Hi\" extra trailing\n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>An unclosed title is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedTitleRejected()
    {
        const string Input = "!!! note \"unclosed\n    body\n";
        await Assert.That(Rewrite(Input)).IsEqualTo(Input);
    }

    /// <summary>Helper that runs the rewriter and returns the string result.</summary>
    /// <param name="source">UTF-8 source markdown.</param>
    /// <returns>Rewritten output.</returns>
    private static string Rewrite(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        AdmonitionRewriter.Rewrite(Encoding.UTF8.GetBytes(source), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
