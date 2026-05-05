// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.SmartSymbols.Tests;

/// <summary>Behavior tests for <c>SmartSymbolsRewriter</c>.</summary>
public class SmartSymbolsRewriterTests
{
    /// <summary><c>(c)</c>, <c>(r)</c>, <c>(tm)</c> rewrite to their Unicode glyphs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ParenSymbolsRewriteCaseInsensitively()
    {
        await Assert.That(Rewrite("Copyright (c) 2026")).IsEqualTo("Copyright © 2026");
        await Assert.That(Rewrite("(R) registered")).IsEqualTo("® registered");
        await Assert.That(Rewrite("Brand (tm)")).IsEqualTo("Brand ™");
        await Assert.That(Rewrite("Brand (TM)")).IsEqualTo("Brand ™");
    }

    /// <summary>Arrow forms — single, double, double-headed.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ArrowFormsRewrite()
    {
        await Assert.That(Rewrite("a --> b")).IsEqualTo("a → b");
        await Assert.That(Rewrite("a <-- b")).IsEqualTo("a ← b");
        await Assert.That(Rewrite("a <--> b")).IsEqualTo("a ↔ b");
        await Assert.That(Rewrite("a ==> b")).IsEqualTo("a ⇒ b");
        await Assert.That(Rewrite("a <== b")).IsEqualTo("a ⇐ b");
        await Assert.That(Rewrite("a <==> b")).IsEqualTo("a ⇔ b");
    }

    /// <summary><c>+/-</c>, <c>=/=</c>, <c>c/o</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SlashSymbolsRewrite()
    {
        await Assert.That(Rewrite("5 +/- 1")).IsEqualTo("5 ± 1");
        await Assert.That(Rewrite("a =/= b")).IsEqualTo("a ≠ b");
        await Assert.That(Rewrite("send c/o the office")).IsEqualTo("send ℅ the office");
    }

    /// <summary>Common fractions rewrite at word boundaries only.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FractionsRewriteAtWordBoundary()
    {
        await Assert.That(Rewrite("1/4 cup")).IsEqualTo("¼ cup");
        await Assert.That(Rewrite("about 1/2 done")).IsEqualTo("about ½ done");
        await Assert.That(Rewrite("3/4 full")).IsEqualTo("¾ full");
    }

    /// <summary>Fractions inside larger numerics are left alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FractionsLeaveLargerNumbersAlone() => await Assert.That(Rewrite("file 21/40 in series")).IsEqualTo("file 21/40 in series");

    /// <summary>Fenced code blocks pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodePassesThrough()
    {
        const string Source = "Before.\n```\n(c) 1/2 -->\n```\nAfter (c).";
        const string Expected = "Before.\n```\n(c) 1/2 -->\n```\nAfter ©.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary>Inline code spans pass through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodePassesThrough() =>
        await Assert.That(Rewrite("use `1/2` literally and 1/2 outside"))
            .IsEqualTo("use `1/2` literally and ½ outside");

    /// <summary>Source with no smart-symbol candidates is byte-for-byte identical.</summary>
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

    /// <summary>Rewrites <paramref name="input"/> via <c>SmartSymbolsRewriter</c> and returns the UTF-8 result decoded to UTF-16.</summary>
    /// <param name="input">Markdown source text.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        SmartSymbolsRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
