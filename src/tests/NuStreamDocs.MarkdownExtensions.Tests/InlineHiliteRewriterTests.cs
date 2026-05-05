// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.InlineHilite;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Behavior tests for <c>InlineHiliteRewriter</c>.</summary>
public class InlineHiliteRewriterTests
{
    /// <summary>A <c>`#!python …`</c> span emits a language-classed <c>code</c> tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShebangInlineCodeWraps() =>
        await Assert.That(Rewrite("call `#!python foo()` here"))
            .IsEqualTo("call <code class=\"highlight language-python\">foo()</code> here");

    /// <summary>HTML-special characters in the code body are escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlSpecialCharsAreEscaped() =>
        await Assert.That(Rewrite("`#!html <p class=\"x\">&hi</p>`"))
            .IsEqualTo("<code class=\"highlight language-html\">&lt;p class=&quot;x&quot;&gt;&amp;hi&lt;/p&gt;</code>");

    /// <summary>Plain inline code without the shebang passes through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainInlineCodePassesThrough() => await Assert.That(Rewrite("a `plain` span")).IsEqualTo("a `plain` span");

    /// <summary>Multi-backtick spans (<c>``…``</c>) are matched by run width.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DoubleBacktickShebangWraps() =>
        await Assert.That(Rewrite("``#!c++ a + b``"))
            .IsEqualTo("<code class=\"highlight language-c++\">a + b</code>");

    /// <summary>Shebang with no trailing space (no code body) passes through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShebangWithoutCodePassesThrough() => await Assert.That(Rewrite("`#!python`")).IsEqualTo("`#!python`");

    /// <summary>Fenced code blocks pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "Outside `#!js x()`.\n```\n`#!python y()`\n```\nAfter `#!js z()`.";
        await Assert.That(Rewrite(Source))
            .IsEqualTo("Outside <code class=\"highlight language-js\">x()</code>.\n```\n`#!python y()`\n```\nAfter <code class=\"highlight language-js\">z()</code>.");
    }

    /// <summary>Plain text round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "No code spans at all.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Rewrites <paramref name="input"/> via <c>InlineHiliteRewriter</c>.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        InlineHiliteRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
