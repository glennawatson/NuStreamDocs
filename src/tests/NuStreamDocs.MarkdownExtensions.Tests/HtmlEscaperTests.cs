// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.MarkdownExtensions.Internal;

namespace NuStreamDocs.MarkdownExtensions.Tests;

/// <summary>Tests for the shared MarkdownExtensions HtmlEscaper.</summary>
public class HtmlEscaperTests
{
    /// <summary>Each special byte expands to the matching named entity.</summary>
    /// <param name="source">Single-byte input.</param>
    /// <param name="expected">Expected entity.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("&", "&amp;")]
    [Arguments("<", "&lt;")]
    [Arguments(">", "&gt;")]
    [Arguments("\"", "&quot;")]
    public async Task SpecialBytesEscape(string source, string expected)
    {
        var sink = new ArrayBufferWriter<byte>();
        HtmlEscaper.Escape(Encoding.UTF8.GetBytes(source), sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(expected);
    }

    /// <summary>Non-special bytes are written verbatim.</summary>
    /// <param name="source">Plain text.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("hello")]
    [Arguments("a")]
    [Arguments("z!@#$%")]
    [Arguments("apostrophe '")]
    public async Task PlainBytesPassThrough(string source)
    {
        var sink = new ArrayBufferWriter<byte>();
        HtmlEscaper.Escape(Encoding.UTF8.GetBytes(source), sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo(source);
    }

    /// <summary>Mixed plain + special bytes alternate correctly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedRun()
    {
        var sink = new ArrayBufferWriter<byte>();
        HtmlEscaper.Escape("a&b<c>d\"e"u8, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("a&amp;b&lt;c&gt;d&quot;e");
    }

    /// <summary>Empty input writes nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInput()
    {
        var sink = new ArrayBufferWriter<byte>();
        HtmlEscaper.Escape(default, sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }
}
