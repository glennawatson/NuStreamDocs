// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.MagicLink.Tests;

/// <summary>Behavior tests for <c>MagicLinkRewriter</c>.</summary>
public class MagicLinkRewriterTests
{
    /// <summary>Bare <c>https://</c> URLs are wrapped in autolink brackets.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HttpsBareUrlIsAutolinked() =>
        await Assert.That(Rewrite("see https://example.com for more"))
            .IsEqualTo("see <https://example.com> for more");

    /// <summary>Trailing sentence punctuation is excluded from the URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingPunctuationIsTrimmed()
    {
        await Assert.That(Rewrite("(see https://example.com).")).IsEqualTo("(see <https://example.com>).");
        await Assert.That(Rewrite("visit https://example.com, today")).IsEqualTo("visit <https://example.com>, today");
    }

    /// <summary><c>http://</c>, <c>ftp://</c>, <c>ftps://</c>, <c>mailto:</c> all autolink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleSchemesAutolink()
    {
        await Assert.That(Rewrite("http://x.test")).IsEqualTo("<http://x.test>");
        await Assert.That(Rewrite("ftp://x.test/file.zip")).IsEqualTo("<ftp://x.test/file.zip>");
        await Assert.That(Rewrite("ftps://x.test")).IsEqualTo("<ftps://x.test>");
        await Assert.That(Rewrite("mailto:foo@bar.test")).IsEqualTo("<mailto:foo@bar.test>");
    }

    /// <summary>URLs already wrapped in autolink brackets are left alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExistingAutolinkPassesThrough() =>
        await Assert.That(Rewrite("see <https://example.com> here"))
            .IsEqualTo("see <https://example.com> here");

    /// <summary>URLs inside markdown link destinations are not double-wrapped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkdownLinkDestinationLeftAlone() =>
        await Assert.That(Rewrite("[label](https://example.com)"))
            .IsEqualTo("[label](https://example.com)");

    /// <summary>Fenced code blocks pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "Before.\n```\nhttps://example.com\n```\nAfter https://example.com.";
        const string Expected = "Before.\n```\nhttps://example.com\n```\nAfter <https://example.com>.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary>Inline code spans pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassesThrough() =>
        await Assert.That(Rewrite("look at `https://example.com` and https://example.com"))
            .IsEqualTo("look at `https://example.com` and <https://example.com>");

    /// <summary>Source with no URLs round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "Just a normal paragraph with no funny business.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input produces empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Rewrites <paramref name="input"/> via <c>MagicLinkRewriter</c> and returns the UTF-8 result.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        MagicLinkRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
