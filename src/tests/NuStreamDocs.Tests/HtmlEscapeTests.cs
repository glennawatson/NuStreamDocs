// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Html;

namespace NuStreamDocs.Tests;

/// <summary>Direct unit tests for the byte-level HtmlEscape helpers.</summary>
public class HtmlEscapeTests
{
    /// <summary>EscapeText replaces every text-content escapable byte.</summary>
    /// <param name="source">Raw UTF-8 input.</param>
    /// <param name="expected">Expected escaped UTF-8 output.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("plain", "plain")]
    [Arguments("a & b", "a &amp; b")]
    [Arguments("a < b > c", "a &lt; b &gt; c")]
    [Arguments("say \"hi\"", "say &quot;hi&quot;")]
    [Arguments("&<>\"", "&amp;&lt;&gt;&quot;")]
    [Arguments("", "")]
    public async Task EscapeTextByte(string source, string expected)
    {
        ArrayBufferWriter<byte> sink = new();
        HtmlEscape.EscapeText(Encoding.UTF8.GetBytes(source), sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(expected);
    }

    /// <summary>EscapeAttribute escapes only <c>&amp;</c> and <c>"</c>; leaves <c>&lt;</c>/<c>&gt;</c> literal.</summary>
    /// <param name="source">Raw UTF-8 input.</param>
    /// <param name="expected">Expected escaped UTF-8 output.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("plain", "plain")]
    [Arguments("a & b", "a &amp; b")]
    [Arguments("a < b > c", "a < b > c")]
    [Arguments("say \"hi\"", "say &quot;hi&quot;")]
    [Arguments("&<>\"", "&amp;<>&quot;")]
    [Arguments("", "")]
    public async Task EscapeAttribute(string source, string expected)
    {
        ArrayBufferWriter<byte> sink = new();
        HtmlEscape.EscapeAttribute(Encoding.UTF8.GetBytes(source), sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(expected);
    }

    /// <summary>EscapeAttribute leaves multi-byte UTF-8 sequences untouched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EscapeAttributeMultiByteUntouched()
    {
        ArrayBufferWriter<byte> sink = new();
        HtmlEscape.EscapeAttribute("café \"日本語\""u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("café &quot;日本語&quot;");
    }

    /// <summary>EscapeText (UTF-16) writes the same bytes as the byte overload.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EscapeTextCharsMatchesBytes()
    {
        const string Source = "a & b < c > d \"e\"";
        ArrayBufferWriter<byte> charSink = new();
        HtmlEscape.EscapeText(Source.AsSpan(), charSink);
        ArrayBufferWriter<byte> byteSink = new();
        HtmlEscape.EscapeText(Encoding.UTF8.GetBytes(Source), byteSink);
        await Assert.That(charSink.WrittenSpan.SequenceEqual(byteSink.WrittenSpan)).IsTrue();
    }

    /// <summary>EscapeText null-checks its sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EscapeTextByteNullSinkThrows() =>
        await Assert.That(() => HtmlEscape.EscapeText("x"u8, null!))
            .Throws<ArgumentNullException>();

    /// <summary>EscapeAttribute null-checks its sink.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EscapeAttributeNullSinkThrows() =>
        await Assert.That(() => HtmlEscape.EscapeAttribute("x"u8, null!))
            .Throws<ArgumentNullException>();
}
