// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Branch-coverage tests for BlockScanner edge cases.</summary>
public class BlockScannerBranchTests
{
    /// <summary>Empty input emits no blocks.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput()
    {
        var blocks = Scan(string.Empty);
        await Assert.That(blocks.Length).IsEqualTo(0);
    }

    /// <summary>ATX heading levels 1..6 and "too deep" 7 hashes are recognised differently.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AtxHeadingLevels()
    {
        var blocks = Scan("# h1\n## h2\n###### h6\n####### too deep\n");
        var headings = 0;
        for (var i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].Kind == BlockKind.AtxHeading)
            {
                headings++;
            }
        }

        // h1, h2, h6 should be ATX; the 7-hash form is a paragraph.
        await Assert.That(headings).IsEqualTo(3);
    }

    /// <summary>Bullet list items at multiple indentation levels.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BulletListVariants()
    {
        var blocks = Scan("- a\n* b\n+ c\n");
        await Assert.That(blocks.Length).IsGreaterThan(0);
    }

    /// <summary>Ordered list items.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OrderedList()
    {
        var blocks = Scan("1. a\n2. b\n3. c\n");
        await Assert.That(blocks.Length).IsGreaterThan(0);
    }

    /// <summary>Tilde fenced code with mismatched length opener and closer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TildeFenceShortCloser()
    {
        var blocks = Scan("~~~\nbody\n~~\nstill body\n~~~\n");
        var fences = 0;
        var content = 0;
        for (var i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].Kind == BlockKind.FencedCode)
            {
                fences++;
            }

            if (blocks[i].Kind == BlockKind.FencedCodeContent)
            {
                content++;
            }
        }

        await Assert.That(fences).IsEqualTo(2);
        await Assert.That(content).IsGreaterThan(0);
    }

    /// <summary>Backtick fence with matching length closer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BacktickFence()
    {
        var blocks = Scan("````\ninner\n````\n");
        var fences = 0;
        for (var i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].Kind == BlockKind.FencedCode)
            {
                fences++;
            }
        }

        await Assert.That(fences).IsEqualTo(2);
    }

    /// <summary>Blank lines emit Blank blocks between paragraphs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlankBetweenParagraphs()
    {
        var blocks = Scan("p1\n\np2\n");
        var blanks = 0;
        var paras = 0;
        for (var i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].Kind == BlockKind.Blank)
            {
                blanks++;
            }

            if (blocks[i].Kind == BlockKind.Paragraph)
            {
                paras++;
            }
        }

        await Assert.That(blanks).IsGreaterThan(0);
        await Assert.That(paras).IsEqualTo(2);
    }

    /// <summary>Helper that runs BlockScanner.Scan and returns the blocks.</summary>
    /// <param name="markdown">UTF-8 markdown source.</param>
    /// <returns>Block descriptors.</returns>
    private static BlockSpan[] Scan(string markdown)
    {
        var sink = new ArrayBufferWriter<BlockSpan>();
        BlockScanner.Scan(Encoding.UTF8.GetBytes(markdown), sink);
        return [.. sink.WrittenSpan];
    }
}
