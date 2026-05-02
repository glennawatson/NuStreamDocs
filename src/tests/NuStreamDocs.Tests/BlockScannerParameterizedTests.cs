// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Parameterized inputs covering BlockScanner heading, list, and fence variants.</summary>
public class BlockScannerParameterizedTests
{
    /// <summary>Each ATX heading level produces an AtxHeading block with the right level.</summary>
    /// <param name="hashes">ATX prefix.</param>
    /// <param name="expectedLevel">Expected level recorded on the block.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("#", 1)]
    [Arguments("##", 2)]
    [Arguments("###", 3)]
    [Arguments("####", 4)]
    [Arguments("#####", 5)]
    [Arguments("######", 6)]
    public async Task AtxHeadingLevels(string hashes, int expectedLevel)
    {
        var blocks = Scan($"{hashes} title\n");
        await Assert.That(blocks.Length).IsEqualTo(1);
        await Assert.That(blocks[0].Kind).IsEqualTo(BlockKind.AtxHeading);
        await Assert.That(blocks[0].Level).IsEqualTo(expectedLevel);
    }

    /// <summary>Bullet markers <c>-</c>, <c>*</c>, <c>+</c> all open a list.</summary>
    /// <param name="marker">Bullet marker character.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("-")]
    [Arguments("*")]
    [Arguments("+")]
    public async Task BulletMarkers(string marker)
    {
        var blocks = Scan($"{marker} item\n");
        await Assert.That(blocks.Length).IsGreaterThan(0);
    }

    /// <summary>Each fence length 3..6 round-trips through Scan as opener + content + closer.</summary>
    /// <param name="length">Number of backticks in the opener and closer.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    [Arguments(6)]
    public async Task BacktickFenceLengths(int length)
    {
        var fence = new string('`', length);
        var blocks = Scan($"{fence}\nbody\n{fence}\n");
        var fences = 0;
        var content = 0;
        for (var i = 0; i < blocks.Length; i++)
        {
            switch (blocks[i].Kind)
            {
                case BlockKind.FencedCode:
                {
                    fences++;
                    break;
                }

                case BlockKind.FencedCodeContent:
                {
                    content++;
                    break;
                }
            }
        }

        await Assert.That(fences).IsEqualTo(2);
        await Assert.That(content).IsEqualTo(1);
    }

    /// <summary>Each tilde fence length round-trips the same way.</summary>
    /// <param name="length">Number of tildes.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(3)]
    [Arguments(4)]
    [Arguments(5)]
    public async Task TildeFenceLengths(int length)
    {
        var fence = new string('~', length);
        var blocks = Scan($"{fence}\nbody\n{fence}\n");
        var fences = 0;
        for (var i = 0; i < blocks.Length; i++)
        {
            if (blocks[i].Kind is BlockKind.FencedCode)
            {
                fences++;
            }
        }

        await Assert.That(fences).IsEqualTo(2);
    }

    /// <summary>An ATX line with too many hashes degrades to a paragraph rather than a heading.</summary>
    /// <param name="hashes">ATX prefix beyond the legal 1..6 range.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("#######")]
    [Arguments("########")]
    public async Task TooManyHashesIsParagraph(string hashes)
    {
        var blocks = Scan($"{hashes} text\n");
        await Assert.That(blocks.Length).IsGreaterThan(0);
        for (var i = 0; i < blocks.Length; i++)
        {
            await Assert.That(blocks[i].Kind).IsNotEqualTo(BlockKind.AtxHeading);
        }
    }

    /// <summary>Helper that runs BlockScanner.Scan over the UTF-8 bytes of <paramref name="markdown"/>.</summary>
    /// <param name="markdown">Source markdown.</param>
    /// <returns>Block descriptors.</returns>
    private static BlockSpan[] Scan(string markdown)
    {
        var sink = new ArrayBufferWriter<BlockSpan>();
        BlockScanner.Scan(Encoding.UTF8.GetBytes(markdown), sink);
        return [.. sink.WrittenSpan];
    }
}
