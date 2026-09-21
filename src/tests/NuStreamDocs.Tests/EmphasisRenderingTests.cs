// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Emphasis and strong rendering through the public <c>MarkdownRenderer</c> entry point.</summary>
public class EmphasisRenderingTests
{
    /// <summary>Repetitions of an unmatched opener in the pathological-input test.</summary>
    private const int UnmatchedRepeats = 4000;

    /// <summary>Emphasis, strong, combined, and nested spans render; a triple run nests strong inside emphasis.</summary>
    /// <param name="markdown">Source text.</param>
    /// <param name="expected">Expected paragraph content.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("*a*", "<em>a</em>")]
    [Arguments("_a_", "<em>a</em>")]
    [Arguments("**a**", "<strong>a</strong>")]
    [Arguments("__a__", "<strong>a</strong>")]
    [Arguments("***a***", "<em><strong>a</strong></em>")]
    [Arguments("___a___", "<em><strong>a</strong></em>")]
    [Arguments("***strong and emphasized***", "<em><strong>strong and emphasized</strong></em>")]
    [Arguments("a***b***c", "a<em><strong>b</strong></em>c")]
    [Arguments(
        "This is ***strong and emphasized*** text and ___this too___.",
        "This is <em><strong>strong and emphasized</strong></em> text and <em><strong>this too</strong></em>.")]
    [Arguments("foo*bar*baz", "foo<em>bar</em>baz")]
    [Arguments("**a**b", "<strong>a</strong>b")]
    [Arguments("__init__", "<strong>init</strong>")]
    [Arguments("*a **b** c*", "<em>a <strong>b</strong> c</em>")]
    [Arguments("**a *b* c**", "<strong>a <em>b</em> c</strong>")]
    [Arguments("***a** b*", "<em><strong>a</strong> b</em>")]
    [Arguments("***a* b**", "<strong><em>a</em> b</strong>")]
    [Arguments("_a *b* c_", "<em>a <em>b</em> c</em>")]
    public async Task SpansRender(string markdown, string expected) =>
        await Assert.That(Render(markdown)).IsEqualTo($"<p>{expected}</p>\n");

    /// <summary>Markers that cannot open or close stay literal.</summary>
    /// <param name="markdown">Source text.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("a * b * c")]
    [Arguments("a _ b _ c")]
    [Arguments("**unclosed")]
    [Arguments("*unclosed")]
    [Arguments("unclosed**")]
    [Arguments("snake_case_word")]
    [Arguments("a_b_")]
    [Arguments("a * b")]
    public async Task UnmatchedOrMisplacedMarkersStayLiteral(string markdown) =>
        await Assert.That(Render(markdown)).IsEqualTo($"<p>{markdown}</p>\n");

    /// <summary>A shorter closing run leaves the surplus opening markers literal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SurplusOpeningMarkerStaysLiteral() =>
        await Assert.That(Render("**a*")).IsEqualTo("<p>*<em>a</em></p>\n");

    /// <summary>A marker inside a code span does not close the emphasis.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CodeSpanHidesMarker() =>
        await Assert.That(Render("*a `*` b*")).IsEqualTo("<p><em>a <code>*</code> b</em></p>\n");

    /// <summary>An escaped marker does not close the emphasis.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EscapedMarkerDoesNotClose() =>
        await Assert.That(Render("*a\\*b*")).IsEqualTo("<p><em>a*b</em></p>\n");

    /// <summary>A multi-byte word character next to an underscore counts as part of the word.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonAsciiLetterBlocksIntraWordUnderscore() =>
        await Assert.That(Render("é_a_é")).IsEqualTo("<p>é_a_é</p>\n");

    /// <summary>Thousands of unmatched openers finish and stay literal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ManyUnmatchedOpenersStayLiteral()
    {
        var markdown = string.Concat(Enumerable.Repeat("*a ", UnmatchedRepeats));
        var html = Render(markdown);
        await Assert.That(html).DoesNotContain("<em>");
        await Assert.That(html).StartsWith("<p>*a *a");
    }

    /// <summary>Renders <paramref name="markdown"/> to an HTML string.</summary>
    /// <param name="markdown">Markdown text.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(string markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render(Encoding.UTF8.GetBytes(markdown), writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
