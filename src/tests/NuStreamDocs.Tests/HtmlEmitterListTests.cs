// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Ordered, bullet, and nested list rendering through the public <c>MarkdownRenderer</c> entry point.</summary>
public class HtmlEmitterListTests
{
    /// <summary>A run of numbered items renders as a single <c>&lt;ol&gt;</c> without the literal markers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OrderedListRendersAsOl()
    {
        var html = Render("1. one\n2. two\n3. three"u8);
        await Assert.That(html).IsEqualTo("<ol>\n<li>one</li>\n<li>two</li>\n<li>three</li>\n</ol>\n");
    }

    /// <summary>An ordered list that does not begin at 1 carries a <c>start</c> attribute.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OrderedListKeepsStartNumber()
    {
        var html = Render("4. four\n5. five"u8);
        await Assert.That(html).IsEqualTo("<ol start=\"4\">\n<li>four</li>\n<li>five</li>\n</ol>\n");
    }

    /// <summary>A bullet list renders as <c>&lt;ul&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BulletListRendersAsUl()
    {
        var html = Render("- one\n- two"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>one</li>\n<li>two</li>\n</ul>\n");
    }

    /// <summary>A bullet list directly followed by a numbered list renders as two separate lists.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ChangingMarkerKindStartsNewList()
    {
        var html = Render("- one\n1. two"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>one</li>\n</ul>\n<ol>\n<li>two</li>\n</ol>\n");
    }

    /// <summary>A numbered list indented under a numbered item renders inside that item's <c>&lt;li&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OrderedListNestsInsideOrderedItem()
    {
        var html = Render("1. Warning\n    1. Event: a\n    2. Consequence: b\n2. Suspension\n    1. Event: c"u8);
        await Assert.That(html).IsEqualTo(
            "<ol>\n"
            + "<li>Warning\n<ol>\n<li>Event: a</li>\n<li>Consequence: b</li>\n</ol>\n</li>\n"
            + "<li>Suspension\n<ol>\n<li>Event: c</li>\n</ol>\n</li>\n"
            + "</ol>\n");
    }

    /// <summary>A bullet list indented under a bullet item renders as a nested <c>&lt;ul&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BulletListNestsInsideBulletItem()
    {
        var html = Render("- a\n  - b\n  - c\n- d"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>a\n<ul>\n<li>b</li>\n<li>c</li>\n</ul>\n</li>\n<li>d</li>\n</ul>\n");
    }

    /// <summary>Lists nest to arbitrary depth.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ListsNestThreeLevelsDeep()
    {
        var html = Render("- a\n  - b\n    - c"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>a\n<ul>\n<li>b\n<ul>\n<li>c</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n");
    }

    /// <summary>An item's own text stays before the nested list even when the item spans several lines.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ItemTextPrecedesNestedList()
    {
        var html = Render("1. first\n   more text\n   - inner"u8);
        await Assert.That(html).IsEqualTo(
            "<ol>\n<li>first\nmore text\n<ul>\n<li>inner</li>\n</ul>\n</li>\n</ol>\n");
    }

    /// <summary>A loose item wraps its own text in a paragraph and still nests the following list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LooseItemNestsList()
    {
        var html = Render("- a\n\n  more\n  - b"u8);
        await Assert.That(html).Contains("<li>\n<p>a</p>\n<p>more</p>\n<ul>\n<li>b</li>\n</ul>\n</li>");
    }

    /// <summary>A thematic-break shaped line inside an item is not mistaken for a nested list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ThematicBreakInsideItemIsNotNestedList()
    {
        var html = Render("- a\n\n  ---\n\n  b"u8);
        await Assert.That(html).DoesNotContain("<ul>\n<li>-");
        await Assert.That(html).Contains("<hr />");
    }

    /// <summary>A digit run without a closing delimiter inside an item is plain text, not a nested list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DigitsWithoutDelimiterAreNotNestedList()
    {
        var html = Render("- a\n  2019 was a year"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>a\n2019 was a year</li>\n</ul>\n");
    }

    /// <summary>A marker that is not followed by whitespace is plain text, not a nested list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkerWithoutTrailingSpaceIsNotNestedList()
    {
        var html = Render("- a\n  -b\n  1.5 kg"u8);
        await Assert.That(html).DoesNotContain("<ul>\n<li>b");
        await Assert.That(html).DoesNotContain("<ol");
    }

    /// <summary>A fenced code block indented under a tight item renders as a code block inside the item.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeInsideTightItemRendersAsCodeBlock()
    {
        var html = Render("1. step\n   ```bash\n   dotnet build\n   ```\n2. next"u8);
        await Assert.That(html).IsEqualTo(
            "<ol>\n<li>step\n<pre><code class=\"language-bash\">dotnet build\n</code></pre>\n</li>\n<li>next</li>\n</ol>\n");
    }

    /// <summary>A blank line before the fence makes the list loose and the fence still renders as code.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeInsideLooseItemRendersAsCodeBlock()
    {
        var html = Render("1. step\n\n   ```bash\n   dotnet build\n   ```\n2. next"u8);
        await Assert.That(html).IsEqualTo(
            "<ol>\n<li>\n<p>step</p>\n<pre><code class=\"language-bash\">dotnet build\n</code></pre>\n</li>\n<li>\n<p>next</p>\n</li>\n</ol>\n");
    }

    /// <summary>Blank lines inside a fenced code block do not make the list loose.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankLineInsideFencedCodeKeepsListTight()
    {
        var html = Render("- step\n  ```\n  a\n\n  b\n  ```\n- next"u8);
        await Assert.That(html).Contains("<li>step\n<pre><code>a\n\nb\n</code></pre>\n</li>");
        await Assert.That(html).Contains("<li>next</li>");
    }

    /// <summary>An ATX heading inside an item renders as a heading.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HeadingInsideItemRendersAsHeading()
    {
        var html = Render("- a\n  # title\n- b"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>a\n<h1>title</h1>\n</li>\n<li>b</li>\n</ul>\n");
    }

    /// <summary>An item that opens with a nested list renders that list as its first child.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ItemOpeningWithListRendersNestedList()
    {
        var html = Render("- - a\n  - b"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>\n<ul>\n<li>a</li>\n<li>b</li>\n</ul>\n</li>\n</ul>\n");
    }

    /// <summary>A blank line between an item's text and its nested list makes the list loose.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankBeforeNestedListMakesListLoose()
    {
        var html = Render("- a\n\n  - b\n- c"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>\n<p>a</p>\n<ul>\n<li>b</li>\n</ul>\n</li>\n<li>\n<p>c</p>\n</li>\n</ul>\n");
    }

    /// <summary>A blank line between two items makes every item in the list loose.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankBetweenItemsMakesEveryItemLoose()
    {
        var html = Render("- a\n- b\n\n- c"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n</li>\n<li>\n<p>c</p>\n</li>\n</ul>\n");
    }

    /// <summary>A blank line inside a nested list does not make the outer list loose.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LooseNestedListKeepsOuterListTight()
    {
        var html = Render("- a\n  - b\n\n  - c\n- d"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>a\n<ul>\n<li>\n<p>b</p>\n</li>\n<li>\n<p>c</p>\n</li>\n</ul>\n</li>\n<li>d</li>\n</ul>\n");
    }

    /// <summary>An HTML block indented under an item is emitted verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlBlockInsideItemIsVerbatim()
    {
        var html = Render("- a\n  <div>raw</div>"u8);
        await Assert.That(html).Contains("<div>raw</div>");
        await Assert.That(html).DoesNotContain("&lt;div");
    }

    /// <summary>A block quote indented under an item renders as a block quote inside the item.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockQuoteInsideItemRendersAsBlockQuote()
    {
        var html = Render("- a\n  > quote\n- b"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>a\n<blockquote>\n<p>quote</p>\n</blockquote>\n</li>\n<li>b</li>\n</ul>\n");
    }

    /// <summary>A lone <c>-</c> line opens an empty bullet item.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoneHyphenOpensEmptyItem()
    {
        var html = Render("-\n- b"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li></li>\n<li>b</li>\n</ul>\n");
    }

    /// <summary>An empty item renders an empty <c>&lt;li&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyItemRendersEmptyLi()
    {
        var html = Render("*\n* b"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li></li>\n<li>b</li>\n</ul>\n");
    }

    /// <summary>Renders <paramref name="markdown"/> to an HTML string.</summary>
    /// <param name="markdown">UTF-8 markdown.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(ReadOnlySpan<byte> markdown)
    {
        ArrayBufferWriter<byte> writer = new();
        MarkdownRenderer.Render(markdown, writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
