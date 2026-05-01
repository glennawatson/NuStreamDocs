// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.MagicLink.Tests;

/// <summary>Branch-coverage edge cases for MagicLinkRewriter.</summary>
public class MagicLinkRewriterBranchTests
{
    /// <summary>Each recognised scheme is wrapped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SchemesWrapped()
    {
        await Assert.That(Rewrite("see https://x.test")).Contains("<https://x.test>");
        await Assert.That(Rewrite("see http://x.test")).Contains("<http://x.test>");
        await Assert.That(Rewrite("see ftps://x.test")).Contains("<ftps://x.test>");
        await Assert.That(Rewrite("see ftp://x.test")).Contains("<ftp://x.test>");
        await Assert.That(Rewrite("see mailto:user@x.test")).Contains("<mailto:user@x.test>");
    }

    /// <summary>Bare www. is not rewritten yet.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BareWwwNotRewritten() =>
        await Assert.That(Rewrite("see www.example.test here")).IsEqualTo("see www.example.test here");

    /// <summary>URL with no body byte after scheme is not rewritten.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SchemeWithoutBody()
    {
        var result = Rewrite("see https:// followed");
        await Assert.That(result).DoesNotContain("<https://>");
    }

    /// <summary>URL with trailing punctuation has the punctuation peeled off.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingPunctTrimmed()
    {
        await Assert.That(Rewrite("see https://x.test.")).IsEqualTo("see <https://x.test>.");
        await Assert.That(Rewrite("see https://x.test, more")).Contains("<https://x.test>,");
        await Assert.That(Rewrite("see https://x.test).")).Contains("<https://x.test>");
    }

    /// <summary>URL preceded by a word byte is NOT a boundary, no rewrite.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoBoundaryBefore() =>
        await Assert.That(Rewrite("ahttps://x.test")).IsEqualTo("ahttps://x.test");

    /// <summary>Inside fenced code: pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "```\nhttps://x.test\n```\n";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Inside inline code: pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassesThrough() =>
        await Assert.That(Rewrite("see `https://x.test` here")).IsEqualTo("see `https://x.test` here");

    /// <summary>Already an angle-bracketed autolink stays untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AlreadyAutolink() =>
        await Assert.That(Rewrite("see <https://x.test> there")).IsEqualTo("see <https://x.test> there");

    /// <summary>An open angle without a closing > advances by one byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedAngle()
    {
        var result = Rewrite("a < no close https://x.test");
        await Assert.That(result).Contains("<https://x.test>");
    }

    /// <summary>Markdown link is preserved verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkdownLinkPreserved()
    {
        var result = Rewrite("see [docs](https://x.test) and https://y.test");
        await Assert.That(result).Contains("[docs](https://x.test)");
        await Assert.That(result).Contains("<https://y.test>");
    }

    /// <summary>A bare bracket without ] just advances one byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BareOpenBracket()
    {
        var result = Rewrite("a [ no close https://x.test");
        await Assert.That(result).Contains("<https://x.test>");
    }

    /// <summary>Brackets without (...) just keep the label.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BracketWithoutDest()
    {
        var result = Rewrite("see [label] then https://x.test");
        await Assert.That(result).Contains("<https://x.test>");
    }

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="input">Source text.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        MagicLinkRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
