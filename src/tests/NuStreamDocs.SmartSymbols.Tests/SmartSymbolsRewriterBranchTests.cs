// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.SmartSymbols.Tests;

/// <summary>Branch-coverage edge cases for SmartSymbolsRewriter.</summary>
public class SmartSymbolsRewriterBranchTests
{
    /// <summary>Truncated tokens at end of input do not match.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TruncatedAtEndDoesNotMatch()
    {
        await Assert.That(Rewrite("(c")).IsEqualTo("(c");
        await Assert.That(Rewrite("(t")).IsEqualTo("(t");
        await Assert.That(Rewrite("+/")).IsEqualTo("+/");
        await Assert.That(Rewrite("=/")).IsEqualTo("=/");
        await Assert.That(Rewrite("--")).IsEqualTo("--");
        await Assert.That(Rewrite("<-")).IsEqualTo("<-");
        await Assert.That(Rewrite("<==")).IsEqualTo("⇐");
        await Assert.That(Rewrite("c/")).IsEqualTo("c/");
        await Assert.That(Rewrite("1/")).IsEqualTo("1/");
        await Assert.That(Rewrite("3/")).IsEqualTo("3/");
    }

    /// <summary>Wrong middle character on paren-tokens leaves text untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ParenWrongChar()
    {
        await Assert.That(Rewrite("(x)")).IsEqualTo("(x)");
        await Assert.That(Rewrite("(c]")).IsEqualTo("(c]");
        await Assert.That(Rewrite("(tx)")).IsEqualTo("(tx)");
        await Assert.That(Rewrite("(tm]")).IsEqualTo("(tm]");
    }

    /// <summary>Plus / equals tokens with wrong following bytes pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PlusEqualsWrongTail()
    {
        await Assert.That(Rewrite("+/x")).IsEqualTo("+/x");
        await Assert.That(Rewrite("+x-")).IsEqualTo("+x-");
        await Assert.That(Rewrite("=x=")).IsEqualTo("=x=");
        await Assert.That(Rewrite("==x")).IsEqualTo("==x");
    }

    /// <summary>Dash and lt tokens with wrong following bytes pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DashAndLtWrongTail()
    {
        await Assert.That(Rewrite("-x>")).IsEqualTo("-x>");
        await Assert.That(Rewrite("--x")).IsEqualTo("--x");
        await Assert.That(Rewrite("<x-")).IsEqualTo("<x-");
        await Assert.That(Rewrite("<-=")).IsEqualTo("<-=");
        await Assert.That(Rewrite("<=-")).IsEqualTo("<=-");
    }

    /// <summary>3/4 only fires at a word boundary.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ThreeQuartersBoundary()
    {
        await Assert.That(Rewrite("3/4")).IsEqualTo("¾");
        await Assert.That(Rewrite("a3/4")).IsEqualTo("a3/4");
        await Assert.That(Rewrite("3/4a")).IsEqualTo("3/4a");
        await Assert.That(Rewrite("3/x")).IsEqualTo("3/x");
    }

    /// <summary>1/2 and 1/4 require word boundary on both sides.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FractionsBoundaryNeg()
    {
        await Assert.That(Rewrite("a1/2")).IsEqualTo("a1/2");
        await Assert.That(Rewrite("1/2a")).IsEqualTo("1/2a");
        await Assert.That(Rewrite("1/9")).IsEqualTo("1/9");
    }

    /// <summary>c/o requires word boundary on both sides.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CareOfBoundary()
    {
        await Assert.That(Rewrite("ac/o")).IsEqualTo("ac/o");
        await Assert.That(Rewrite("c/oa")).IsEqualTo("c/oa");
        await Assert.That(Rewrite("c/x")).IsEqualTo("c/x");
        await Assert.That(Rewrite("C/O")).IsEqualTo("℅");
    }

    /// <summary>Tilde fences pass through.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TildeFences()
    {
        const string Source = "~~~\n(c)\n~~~\nAfter (c).";
        const string Expected = "~~~\n(c)\n~~~\nAfter ©.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary>Helper that runs the rewriter and decodes UTF-8 output.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        SmartSymbolsRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
