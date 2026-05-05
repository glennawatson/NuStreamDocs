// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tags.Tests;

/// <summary>Direct tests for the shared XML/HTML entity escaper.</summary>
public class XmlEntityEscaperTests
{
    /// <summary>Plain text with no special bytes is copied verbatim.</summary>
    /// <param name="mode">Escape mode.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(XmlEntityEscaper.Mode.Xml)]
    [Arguments(XmlEntityEscaper.Mode.HtmlAttribute)]
    public async Task PassThroughPlainText(XmlEntityEscaper.Mode mode)
    {
        ArrayBufferWriter<byte> sink = new();
        XmlEntityEscaper.WriteEscaped(sink, "hello world"u8, mode);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("hello world");
    }

    /// <summary>The minimal XML mode escapes ampersand, less-than, greater-than but leaves quotes alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task XmlModeKeepsQuotesUnescaped()
    {
        ArrayBufferWriter<byte> sink = new();
        XmlEntityEscaper.WriteEscaped(sink, "a & b < c > d \" e"u8, XmlEntityEscaper.Mode.Xml);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("a &amp; b &lt; c &gt; d \" e");
    }

    /// <summary>The HTML attribute mode escapes the same set plus the double quote.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HtmlAttributeModeEscapesQuotes()
    {
        ArrayBufferWriter<byte> sink = new();
        XmlEntityEscaper.WriteEscaped(sink, "a & b < c > d \" e"u8, XmlEntityEscaper.Mode.HtmlAttribute);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("a &amp; b &lt; c &gt; d &quot; e");
    }

    /// <summary>An empty input writes nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput()
    {
        ArrayBufferWriter<byte> sink = new();
        XmlEntityEscaper.WriteEscaped(sink, default, XmlEntityEscaper.Mode.Xml);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Special bytes at the very start and end of the buffer flush correctly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SpecialAtBoundaries()
    {
        ArrayBufferWriter<byte> sink = new();
        XmlEntityEscaper.WriteEscaped(sink, "<a&b>"u8, XmlEntityEscaper.Mode.Xml);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("&lt;a&amp;b&gt;");
    }

    /// <summary>A null writer throws.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullWriterThrows() =>
        await Assert.That(static () => XmlEntityEscaper.WriteEscaped(null!, default, XmlEntityEscaper.Mode.Xml))
            .Throws<ArgumentNullException>();
}
