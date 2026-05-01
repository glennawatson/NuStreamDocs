// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.MdInHtml;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behaviour tests for <c>MdInHtmlRewriter</c>.</summary>
public class MdInHtmlRewriterTests
{
    /// <summary>A <c>markdown="1"</c> attribute is stripped and blank lines pad the body.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkdownAttributeIsStrippedAndBodyPadded()
    {
        const string Source = "<div markdown=\"1\">**bold**</div>";
        const string Expected = "<div>\n\n**bold**\n\n</div>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary><c>markdown="block"</c> and <c>markdown="span"</c> both trigger.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockAndSpanValuesTrigger()
    {
        await Assert.That(Rewrite("<section markdown=\"block\">x</section>"))
            .IsEqualTo("<section>\n\nx\n\n</section>");
        await Assert.That(Rewrite("<span markdown=\"span\">x</span>"))
            .IsEqualTo("<span>\n\nx\n\n</span>");
    }

    /// <summary>Other attributes on the open tag are preserved.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OtherAttributesArePreserved()
    {
        const string Source = "<div class=\"note\" markdown=\"1\" id=\"n1\">x</div>";
        const string Expected = "<div class=\"note\" id=\"n1\">\n\nx\n\n</div>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary>Nested same-name tags are matched by depth.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedSameNameTagsBalance()
    {
        const string Source = "<div markdown=\"1\"><div>inner</div>outer</div>";
        const string Expected = "<div>\n\n<div>inner</div>outer\n\n</div>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary>A non-recognised <c>markdown</c> value (e.g. <c>0</c>) leaves the tag alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnrecognisedValueIsIgnored()
    {
        const string Source = "<div markdown=\"0\">leave me</div>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Tags without the attribute pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TagsWithoutAttributeArePreserved()
    {
        const string Source = "<div class=\"x\">**not parsed**</div>";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Fenced code blocks pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "```\n<div markdown=\"1\">x</div>\n```";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Plain text round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "Just regular markdown text.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>An open tag with no matching close passes through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnmatchedOpenPassesThrough()
    {
        const string Source = "<div markdown=\"1\">unterminated";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Less-than not followed by a tag-name letter passes through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LessThanNotTagName() =>
        await Assert.That(Rewrite("<5 less than")).IsEqualTo("<5 less than");

    /// <summary>Open tag without a closer is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OpenTagWithoutClose() =>
        await Assert.That(Rewrite("<div markdown=\"1\"")).Contains("markdown=\"1\"");

    /// <summary>Tag with markdown attribute but no matching close stays untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoMatchingCloseTag() =>
        await Assert.That(Rewrite("<div markdown=\"1\">body")).Contains("markdown=\"1\"");

    /// <summary>Unquoted markdown attribute is not recognised.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnquotedAttribute() =>
        await Assert.That(Rewrite("<div markdown=1>x</div>")).IsEqualTo("<div markdown=1>x</div>");

    /// <summary>Close tag with whitespace inside the angle bracket is recognised.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CloseTagWithWhitespace() =>
        await Assert.That(Rewrite("<div markdown=\"1\">x</div >")).Contains("\n\nx\n\n");

    /// <summary>Close tag with non-whitespace inside the angle bracket is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CloseTagWithGarbage() =>
        await Assert.That(Rewrite("<div markdown=\"1\">x</divx>")).Contains("markdown=\"1\"");

    /// <summary>Tag with tab between attributes is handled.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TagWithTabBetweenAttributes() =>
        await Assert.That(Rewrite("<div\tmarkdown=\"1\">x</div>")).Contains("\n\nx\n\n");

    /// <summary>Open tag with attribute on a new line.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OpenTagWithNewlineWhitespace() =>
        await Assert.That(Rewrite("<div\nmarkdown=\"1\">x</div>")).Contains("\n\nx\n\n");

    /// <summary>Self-closing tag (no body) is left untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SelfClosingNotRewritten() =>
        await Assert.That(Rewrite("<br markdown=\"1\"/>")).Contains("markdown=\"1\"");

    /// <summary>Tag whose name continues with digits and hyphens is recognised.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TagNameWithDigitsAndHyphens() =>
        await Assert.That(Rewrite("<my-tag2 markdown=\"1\">x</my-tag2>")).Contains("\n\nx\n\n");

    /// <summary>Open tag at end of input without close angle is left alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OpenTagAtEndOfInput() =>
        await Assert.That(Rewrite("<div ")).IsEqualTo("<div ");

    /// <summary>Markdown attribute with no value at end of input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkdownAttrAtEnd() =>
        await Assert.That(Rewrite("<div markdown=")).IsEqualTo("<div markdown=");

    /// <summary>Markdown attribute with no closing quote is rejected.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkdownAttrUnclosedQuote() =>
        await Assert.That(Rewrite("<div markdown=\"1>body</div>")).IsEqualTo("<div markdown=\"1>body</div>");

    /// <summary>Rewrites <paramref name="input"/> via <c>MdInHtmlRewriter</c>.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        MdInHtmlRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
