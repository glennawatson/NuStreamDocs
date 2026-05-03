// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Line-level classification tests for <c>BlockScanner</c>.</summary>
public class BlockScannerTests
{
    /// <summary>An ATX line should classify as a heading.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesAtxHeading()
    {
        var kinds = ScanKinds("# Hi\n"u8);
        await Assert.That(kinds.Length).IsEqualTo(1);
        await Assert.That(kinds[0]).IsEqualTo(BlockKind.AtxHeading);
    }

    /// <summary>A thematic break line should classify as a thematic break, not a paragraph.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesThematicBreak()
    {
        var kinds = ScanKinds("---\n"u8);
        await Assert.That(kinds[0]).IsEqualTo(BlockKind.ThematicBreak);
    }

    /// <summary>Setext underlines should classify with their level encoded.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesSetextUnderline()
    {
        var kinds = ScanKinds("Title\n===\n"u8);
        await Assert.That(kinds[1]).IsEqualTo(BlockKind.SetextHeading);
    }

    /// <summary>Lines inside an open fence should be FencedCodeContent regardless of surface shape.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FencedCodeOpensAndClosesAndStraysAreContent()
    {
        var kinds = ScanKinds("```\n# not a heading\n```\n"u8);
        await Assert.That(kinds[0]).IsEqualTo(BlockKind.FencedCode);
        await Assert.That(kinds[1]).IsEqualTo(BlockKind.FencedCodeContent);
        await Assert.That(kinds[2]).IsEqualTo(BlockKind.FencedCode);
    }

    /// <summary>Block quotes, list items, and blank lines should classify correctly.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesBlockQuoteListAndBlank()
    {
        var kinds = ScanKinds("> q\n- item\n1. one\n\n"u8);
        await Assert.That(kinds[0]).IsEqualTo(BlockKind.BlockQuote);
        await Assert.That(kinds[1]).IsEqualTo(BlockKind.ListItem);
        await Assert.That(kinds[2]).IsEqualTo(BlockKind.ListItem);
        await Assert.That(kinds[3]).IsEqualTo(BlockKind.Blank);
    }

    /// <summary>A 4-space-indented line should classify as indented code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassifiesIndentedCode()
    {
        var kinds = ScanKinds("    code\n"u8);
        await Assert.That(kinds[0]).IsEqualTo(BlockKind.IndentedCode);
    }

    /// <summary>Indented lines following a list item should classify as list-item continuation, not indented code.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IndentedLinesAfterListItemBecomeListItemContent()
    {
        var kinds = ScanKinds("-   item\n\n    ---\n\n    body\n"u8);
        await Assert.That(kinds[0]).IsEqualTo(BlockKind.ListItem);
        await Assert.That(kinds[1]).IsEqualTo(BlockKind.Blank);
        await Assert.That(kinds[2]).IsEqualTo(BlockKind.ListItemContent);
        await Assert.That(kinds[3]).IsEqualTo(BlockKind.Blank);
        await Assert.That(kinds[4]).IsEqualTo(BlockKind.ListItemContent);
    }

    /// <summary>An outdented non-list line should close the list and reclassify normally.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OutdentedLineClosesList()
    {
        var kinds = ScanKinds("- item\noutside\n"u8);
        await Assert.That(kinds[0]).IsEqualTo(BlockKind.ListItem);
        await Assert.That(kinds[1]).IsEqualTo(BlockKind.Paragraph);
    }

    /// <summary>Helper that scans <paramref name="utf8"/> and returns the emitted kinds.</summary>
    /// <param name="utf8">UTF-8 source bytes.</param>
    /// <returns>Per-line block kinds.</returns>
    private static BlockKind[] ScanKinds(ReadOnlySpan<byte> utf8)
    {
        var writer = new ArrayBufferWriter<BlockSpan>();
        BlockScanner.Scan(utf8, writer);
        var spans = writer.WrittenSpan;
        var result = new BlockKind[spans.Length];
        for (var i = 0; i < spans.Length; i++)
        {
            result[i] = spans[i].Kind;
        }

        return result;
    }
}
