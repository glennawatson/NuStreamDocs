// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Search.Tests;

/// <summary>Unit tests for <c>HtmlTextExtractor</c>.</summary>
public class HtmlTextExtractorTests
{
    /// <summary>Plain HTML should reduce to its visible text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExtractsVisibleText()
    {
        var (text, title) = Extract("<p>hello <em>world</em></p>"u8);
        await Assert.That(text).IsEqualTo("hello world");
        await Assert.That(title).IsEqualTo(string.Empty);
    }

    /// <summary>The first H1 should land in the title slot, body still includes its text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CapturesFirstH1AsTitle()
    {
        var (text, title) = Extract("<h1>Greetings</h1><p>body text</p><h1>Second</h1>"u8);
        await Assert.That(title).IsEqualTo("Greetings");
        await Assert.That(text).Contains("Greetings");
        await Assert.That(text).Contains("body text");
    }

    /// <summary>Script and style blocks should be dropped from the visible text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DropsScriptAndStyle()
    {
        var (text, _) = Extract("<p>visible</p><script>var hidden=1;</script><style>.x{color:red}</style><p>also visible</p>"u8);
        await Assert.That(text).Contains("visible");
        await Assert.That(text).DoesNotContain("hidden");
        await Assert.That(text).DoesNotContain("color:red");
    }

    /// <summary>Whitespace runs should collapse to a single space.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CollapsesWhitespace()
    {
        var (text, _) = Extract("<p>a  \n\t b</p>"u8);
        await Assert.That(text).IsEqualTo("a b");
    }

    /// <summary>Convenience: extract and decode.</summary>
    /// <param name="html">UTF-8 HTML.</param>
    /// <returns>Tuple of decoded body text and title string.</returns>
    private static (string Text, string Title) Extract(ReadOnlySpan<byte> html)
    {
        ArrayBufferWriter<byte> sink = new();
        var titleBytes = HtmlTextExtractor.Extract(html, sink);
        return (Encoding.UTF8.GetString(sink.WrittenSpan), Encoding.UTF8.GetString(titleBytes));
    }
}
