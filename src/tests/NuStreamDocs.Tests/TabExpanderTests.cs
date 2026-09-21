// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Tab handling in line indentation, at the helper and renderer level.</summary>
public class TabExpanderTests
{
    /// <summary>Source with no tab is reported as not needing expansion.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoTabNeedsNoExpansion() =>
        await Assert.That(TabExpander.MayNeedExpansion("a\n    b"u8)).IsFalse();

    /// <summary>A leading tab becomes four spaces.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LeadingTabBecomesFourSpaces() =>
        await Assert.That(Encoding.UTF8.GetString(TabExpander.Expand("\tx"u8))).IsEqualTo("    x");

    /// <summary>A tab after spaces advances to the next multiple of four.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabAfterSpacesAdvancesToNextStop() =>
        await Assert.That(Encoding.UTF8.GetString(TabExpander.Expand("  \tx"u8))).IsEqualTo("    x");

    /// <summary>Two leading tabs become eight spaces.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoTabsBecomeEightSpaces() =>
        await Assert.That(Encoding.UTF8.GetString(TabExpander.Expand("\t\tx"u8))).IsEqualTo("        x");

    /// <summary>Tabs after the first non-whitespace byte are kept.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InteriorTabIsKept() =>
        await Assert.That(Encoding.UTF8.GetString(TabExpander.Expand("a\tb"u8))).IsEqualTo("a\tb");

    /// <summary>Each line is expanded independently.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EveryLineIsExpanded() =>
        await Assert.That(Encoding.UTF8.GetString(TabExpander.Expand("\ta\n\tb\n"u8))).IsEqualTo("    a\n    b\n");

    /// <summary>A whitespace-only tab line keeps its line ending.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WhitespaceOnlyLineKeepsLineEnding() =>
        await Assert.That(Encoding.UTF8.GetString(TabExpander.Expand("\t\r\nx"u8))).IsEqualTo("    \r\nx");

    /// <summary>A tab-indented line after a blank line renders as an indented code block.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabIndentedLineRendersAsCodeBlock() =>
        await Assert.That(Render("a\n\n\tcode"u8)).IsEqualTo("<p>a</p>\n<pre><code>code\n</code></pre>\n");

    /// <summary>A tab-indented list marker nests under its parent item.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabIndentedListNests() =>
        await Assert.That(Render("- a\n\t- b"u8)).IsEqualTo("<ul>\n<li>a\n<ul>\n<li>b</li>\n</ul>\n</li>\n</ul>\n");

    /// <summary>A tab-indented paragraph continues a list item.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TabIndentedParagraphContinuesListItem() =>
        await Assert.That(Render("- a\n\n\tb"u8)).IsEqualTo("<ul>\n<li>\n<p>a</p>\n<p>b</p>\n</li>\n</ul>\n");

    /// <summary>A tab inside a fenced code line is preserved.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InteriorTabInsideFenceIsPreserved() =>
        await Assert.That(Render("```\na\tb\n```"u8)).IsEqualTo("<pre><code>a\tb\n</code></pre>\n");

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
