// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>Lists and block quotes nested inside each other, rendered through <c>MarkdownRenderer.Render</c>.</summary>
public class NestedContentRenderingTests
{
    /// <summary>Number of items in the large document.</summary>
    private const int LargeDocumentItemCount = 200;

    /// <summary>Number of renders repeated on one thread.</summary>
    private const int RepeatCount = 5;

    /// <summary>Number of renders each concurrent task performs.</summary>
    private const int ConcurrentRenderCount = 200;

    /// <summary>Bullet lists nest six levels deep.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BulletListsNestSixLevelsDeep()
    {
        var html = Render("- a\n  - b\n    - c\n      - d\n        - e\n          - f"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>a\n<ul>\n<li>b\n<ul>\n<li>c\n<ul>\n<li>d\n<ul>\n<li>e\n<ul>\n<li>f</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n");
    }

    /// <summary>Block quotes nest six levels deep.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockQuotesNestSixLevelsDeep()
    {
        var html = Render("> > > > > > deep"u8);
        await Assert.That(html).IsEqualTo(
            "<blockquote>\n<blockquote>\n<blockquote>\n<blockquote>\n<blockquote>\n<blockquote>\n<p>deep</p>\n</blockquote>\n</blockquote>\n</blockquote>\n</blockquote>\n</blockquote>\n</blockquote>\n");
    }

    /// <summary>Quotes and lists alternate through five levels.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task QuotesAndListsAlternateFiveLevelsDeep()
    {
        var html = Render("> - a\n>   > - b\n>   >   > - c"u8);
        await Assert.That(html).IsEqualTo(
            "<blockquote>\n<ul>\n<li>a\n<blockquote>\n<ul>\n<li>b\n<blockquote>\n<ul>\n<li>c</li>\n</ul>\n</blockquote>\n</li>\n</ul>\n</blockquote>\n</li>\n</ul>\n</blockquote>\n");
    }

    /// <summary>A tight nested list keeps its items unwrapped at every depth.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TightNestedListKeepsItemsUnwrapped()
    {
        var html = Render("- a\n  - b\n  - c\n- d"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>a\n<ul>\n<li>b</li>\n<li>c</li>\n</ul>\n</li>\n<li>d</li>\n</ul>\n");
    }

    /// <summary>A blank line between two items makes the list loose and wraps every single-line item in a paragraph.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankBetweenSingleLineItemsWrapsEachInParagraph()
    {
        var html = Render("- a\n\n- b\n\n- c"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n</li>\n<li>\n<p>c</p>\n</li>\n</ul>\n");
    }

    /// <summary>Trailing spaces on a one-line item followed by a blank line are trimmed from its paragraph.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingSpacesOnItemBeforeBlankAreTrimmed()
    {
        var html = Render("- a  \n\n- b"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n</li>\n</ul>\n");
    }

    /// <summary>A one-line item that opens a block keeps the block rendering when a blank line follows it.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockOpeningItemBeforeBlankStillRendersBlock()
    {
        var html = Render("- > quoted\n\n- b"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>\n<blockquote>\n<p>quoted</p>\n</blockquote>\n</li>\n<li>\n<p>b</p>\n</li>\n</ul>\n");
    }

    /// <summary>An item whose children are separated by a blank line makes the whole list loose.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankBetweenItemChildrenMakesListLoose()
    {
        var html = Render("- a\n\n  b\n- c"u8);
        await Assert.That(html).IsEqualTo("<ul>\n<li>\n<p>a</p>\n<p>b</p>\n</li>\n<li>\n<p>c</p>\n</li>\n</ul>\n");
    }

    /// <summary>A blank line inside a nested item makes only the nested list loose in the tight outer list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LooseInnerListInsideTightOuterList()
    {
        var html = Render("- a\n  - b\n\n  - c\n- d"u8);
        await Assert.That(html).IsEqualTo(
            "<ul>\n<li>a\n<ul>\n<li>\n<p>b</p>\n</li>\n<li>\n<p>c</p>\n</li>\n</ul>\n</li>\n<li>d</li>\n</ul>\n");
    }

    /// <summary>Tab-indented nesting five levels deep renders the same as its space-indented twin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabIndentedNestingMatchesSpaceIndentedNesting()
    {
        var spaces = Render("- a\n    - b\n        - c\n            - d\n                - e"u8);
        var tabs = Render("- a\n\t- b\n\t\t- c\n\t\t\t- d\n\t\t\t\t- e"u8);
        await Assert.That(tabs).IsEqualTo(spaces);
        await Assert.That(tabs).Contains("<li>e</li>");
    }

    /// <summary>A block quote lazily continues a paragraph and then holds a nested list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task QuoteWithLazyLineAndNestedList()
    {
        var html = Render("> intro\nlazy\n>\n> - one\n> - two"u8);
        await Assert.That(html).IsEqualTo("<blockquote>\n<p>intro\nlazy</p>\n<ul>\n<li>one</li>\n<li>two</li>\n</ul>\n</blockquote>\n");
    }

    /// <summary>Rendering a large nested document and then a small one on the same thread leaves no residue.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SmallDocumentAfterLargeDocumentRendersCleanly()
    {
        var large = new StringBuilder();
        for (var i = 0; i < LargeDocumentItemCount; i++)
        {
            _ = large.Append("- item ").Append(i).Append("\n  - inner\n    > quoted\n\n");
        }

        var first = Render(Encoding.UTF8.GetBytes(large.ToString()));
        var small = Render("- x\n  - y"u8);
        var second = Render(Encoding.UTF8.GetBytes(large.ToString()));

        await Assert.That(small).IsEqualTo("<ul>\n<li>x\n<ul>\n<li>y</li>\n</ul>\n</li>\n</ul>\n");
        await Assert.That(second).IsEqualTo(first);
    }

    /// <summary>Repeated renders of the same nested document are byte-identical.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepeatedRendersAreIdentical()
    {
        var source = "1. a\n    - b\n        > q\n2. c\n\n    d\n"u8.ToArray();
        var first = Render(source);
        for (var i = 0; i < RepeatCount; i++)
        {
            await Assert.That(Render(source)).IsEqualTo(first);
        }
    }

    /// <summary>Concurrent renders on separate threads produce identical output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConcurrentRendersAreIdentical()
    {
        var source = "1. a\n    - b\n        > q\n2. c\n\n    d\n"u8.ToArray();
        var expected = Render(source);
        var tasks = new Task<string>[8];
        for (var t = 0; t < tasks.Length; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                var last = string.Empty;
                for (var i = 0; i < ConcurrentRenderCount; i++)
                {
                    last = Render(source);
                }

                return last;
            });
        }

        var results = await Task.WhenAll(tasks);
        for (var t = 0; t < results.Length; t++)
        {
            await Assert.That(results[t]).IsEqualTo(expected);
        }
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
