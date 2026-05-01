// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Arithmatex.Tests;

/// <summary>Behaviour tests for <c>ArithmatexRewriter</c>.</summary>
public class ArithmatexRewriterTests
{
    /// <summary>Inline <c>$x$</c> wraps in <c>&lt;span class="arithmatex"&gt;\(x\)&lt;/span&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineMathWraps() =>
        await Assert.That(Rewrite("Euler $e^{i\\pi}+1=0$ is famous"))
            .IsEqualTo("Euler <span class=\"arithmatex\">\\(e^{i\\pi}+1=0\\)</span> is famous");

    /// <summary>Block <c>$$x$$</c> wraps in <c>&lt;div class="arithmatex"&gt;\[x\]&lt;/div&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BlockMathWraps() =>
        await Assert.That(Rewrite(@"$$\int_0^1 x\,dx = 1/2$$"))
            .IsEqualTo("<div class=\"arithmatex\">\\[\\int_0^1 x\\,dx = 1/2\\]</div>");

    /// <summary>Price-like <c>$5</c> does not trigger inline math.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PriceLikeInputIsNotMath() =>
        await Assert.That(Rewrite("It costs $5 or $10 today"))
            .IsEqualTo("It costs $5 or $10 today");

    /// <summary>An open-<c>$</c> followed by whitespace is not math.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WhitespaceAfterOpenIsNotMath() => await Assert.That(Rewrite("a $ x $ b")).IsEqualTo("a $ x $ b");

    /// <summary>Two valid inline math spans on the same line both wrap.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TwoInlineSpansOnOneLine() =>
        await Assert.That(Rewrite("$a$ and $b$"))
            .IsEqualTo("<span class=\"arithmatex\">\\(a\\)</span> and <span class=\"arithmatex\">\\(b\\)</span>");

    /// <summary>Math inside fenced code blocks passes through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "Inline $a$.\n```\n$a$ and $$b$$\n```\nAfter $c$.";
        await Assert.That(Rewrite(Source))
            .IsEqualTo("Inline <span class=\"arithmatex\">\\(a\\)</span>.\n```\n$a$ and $$b$$\n```\nAfter <span class=\"arithmatex\">\\(c\\)</span>.");
    }

    /// <summary>Math inside inline-code spans passes through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassesThrough() =>
        await Assert.That(Rewrite("Use `$a$` literally and $b$ wrapped"))
            .IsEqualTo("Use `$a$` literally and <span class=\"arithmatex\">\\(b\\)</span> wrapped");

    /// <summary>Plain text round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlainTextRoundTrips()
    {
        const string Source = "Just text without any math markers.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Unclosed inline math passes through unchanged.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedInlineMathPassesThrough() => await Assert.That(Rewrite("trailing $x with no close")).IsEqualTo("trailing $x with no close");

    /// <summary>Rewrites <paramref name="input"/> via <c>ArithmatexRewriter</c>.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var sink = new ArrayBufferWriter<byte>(Math.Max(bytes.Length, 1));
        ArithmatexRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
