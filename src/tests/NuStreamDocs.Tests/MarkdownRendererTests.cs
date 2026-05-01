// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Tests;

/// <summary>
/// Smoke tests for the public <c>MarkdownRenderer</c> entry point.
/// </summary>
public class MarkdownRendererTests
{
    /// <summary>An ATX heading should render as the matching <c>&lt;hN&gt;</c> element.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RendersAtxHeading()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("# Hello"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("<h1>");
        await Assert.That(html).Contains("Hello");
    }

    /// <summary>Plain text should render as a <c>&lt;p&gt;</c> with HTML entities escaped.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EscapesParagraphContent()
    {
        var writer = new ArrayBufferWriter<byte>();
        MarkdownRenderer.Render("a < b & c"u8, writer);
        var html = Encoding.UTF8.GetString(writer.WrittenSpan);
        await Assert.That(html).Contains("&lt;");
        await Assert.That(html).Contains("&amp;");
    }
}
