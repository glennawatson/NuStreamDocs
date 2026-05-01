// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Markdown;

namespace NuStreamDocs.Tests;

/// <summary>Tests for the AutoLink scheme/close-byte helpers and the inline-renderer happy path.</summary>
public class AutoLinkTests
{
    /// <summary>Newline aborts the autolink scan with -1.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NewlineAborts() =>
        await Assert.That(AutoLink.FindClose("<https://x.test\n>"u8.ToArray(), 1)).IsEqualTo(-1);

    /// <summary>Carriage return aborts the autolink scan with -1.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CarriageReturnAborts() =>
        await Assert.That(AutoLink.FindClose("<https://x.test\r>"u8.ToArray(), 1)).IsEqualTo(-1);

    /// <summary>Space aborts the autolink scan with -1.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpaceAborts() =>
        await Assert.That(AutoLink.FindClose("<https://x.test >"u8.ToArray(), 1)).IsEqualTo(-1);

    /// <summary>Nested less-than aborts the scan.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedLessThanAborts() =>
        await Assert.That(AutoLink.FindClose("<a<b>"u8.ToArray(), 1)).IsEqualTo(-1);

    /// <summary>End-of-input without close returns -1.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EndOfInputReturnsMinusOne() =>
        await Assert.That(AutoLink.FindClose("<no-close"u8.ToArray(), 1)).IsEqualTo(-1);

    /// <summary>Single-letter scheme is below MinSchemeLength so IsAutolink is false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShortSchemeRejected() =>
        await Assert.That(AutoLink.IsAutolink("a:rest"u8.ToArray())).IsFalse();

    /// <summary>Slice with no colon is not an autolink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoColonRejected() =>
        await Assert.That(AutoLink.IsAutolink("no-colon"u8.ToArray())).IsFalse();

    /// <summary>An angle bracket missing a scheme is not an autolink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoSchemeNotAutolink() =>
        await Assert.That(Render("<no-scheme-here>")).DoesNotContain("href=");

    /// <summary>A scheme that starts with a digit is not a valid autolink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DigitSchemeStart() =>
        await Assert.That(Render("<1http://x>")).DoesNotContain("href=\"1http");

    /// <summary>Multi-character schemes with plus/minus/dot are accepted.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SchemeWithPlusMinusDot()
    {
        await Assert.That(Render("<git+https://example.test>")).Contains("href=\"git+https://example.test\"");
        await Assert.That(Render("<x-foo://example.test>")).Contains("href=\"x-foo://example.test\"");
        await Assert.That(Render("<a.b://example.test>")).Contains("href=\"a.b://example.test\"");
    }

    /// <summary>A bracket with no closing > stays as escaped text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoClosingBracket() =>
        await Assert.That(Render("<https://nope")).Contains("&lt;");

    /// <summary>Render through InlineRenderer captures the happy path with successful close.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HappyPathRender() =>
        await Assert.That(Render("<https://x.test>"))
            .IsEqualTo("<a href=\"https://x.test\">https://x.test</a>");

    /// <summary>IsAutolink accepts schemes ending in each permitted continuation character.</summary>
    /// <param name="content">Slice between the angle brackets.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("a1:rest")]
    [Arguments("a+:rest")]
    [Arguments("a-:rest")]
    [Arguments("a.:rest")]
    [Arguments("aZ:rest")]
    [Arguments("ab9:rest")]
    public async Task IsAutolinkAcceptsEverySchemeChar(string content) =>
        await Assert.That(AutoLink.IsAutolink(Encoding.UTF8.GetBytes(content))).IsTrue();

    /// <summary>IsAutolink rejects schemes with disallowed bytes.</summary>
    /// <param name="content">Slice between the angle brackets.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("a_:rest")]
    [Arguments("a@:rest")]
    [Arguments("a/:rest")]
    [Arguments("a :rest")]
    [Arguments("1ab:rest")]
    public async Task IsAutolinkRejectsDisallowedSchemeBytes(string content) =>
        await Assert.That(AutoLink.IsAutolink(Encoding.UTF8.GetBytes(content))).IsFalse();

    /// <summary>FindClose returns the index of the first <c>&gt;</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FindCloseReturnsImmediateIndex() =>
        await Assert.That(AutoLink.FindClose("ab>"u8.ToArray(), 0)).IsEqualTo(2);

    /// <summary>An empty content (<c>&lt;&gt;</c>) is rejected as a non-autolink and renders as escaped text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyContentRejected()
    {
        await Assert.That(AutoLink.IsAutolink(default)).IsFalse();
        await Assert.That(Render("<>")).Contains("&lt;&gt;");
    }

    /// <summary>Helper that runs InlineRenderer and decodes UTF-8 output.</summary>
    /// <param name="input">Inline source.</param>
    /// <returns>Rendered HTML.</returns>
    private static string Render(string input)
    {
        var sink = new ArrayBufferWriter<byte>();
        InlineRenderer.Render(Encoding.UTF8.GetBytes(input), sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
