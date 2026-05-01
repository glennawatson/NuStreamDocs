// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Toc.Tests;

/// <summary>Direct tests for the TocFragmentRenderer covering nesting and entity-escape branches.</summary>
public class TocFragmentRendererTests
{
    /// <summary>Empty heading list emits nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyEmitsNothing()
    {
        var sink = new ArrayBufferWriter<byte>();
        TocFragmentRenderer.Render([], [], TocOptions.Default, sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Headings outside the level filter emit nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FilteredOut()
    {
        var html = "<h1>x</h1>"u8.ToArray();
        var headings = HeadingScanner.Scan(html);
        var sink = new ArrayBufferWriter<byte>();
        TocFragmentRenderer.Render(html, headings, new(MinLevel: 2, MaxLevel: 6, PermalinkSymbol: "#", MarkerSubstitute: false), sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Nested headings produce nested ul/li with entity escapes for special chars.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedAndEscaped()
    {
        var html = "<h2 id=\"a\">A &amp; B &gt; C</h2><h3 id=\"b\">child</h3><h2 id=\"c\">D</h2>"u8.ToArray();
        var headings = HeadingScanner.Scan(html);
        var slugged = headings.Select(h => h with { Slug = h.ExistingId }).ToArray();
        var sink = new ArrayBufferWriter<byte>();
        TocFragmentRenderer.Render(html, slugged, TocOptions.Default, sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("<nav class=\"md-nav md-nav--secondary\"");
        await Assert.That(output).Contains("md-nav__list");
        await Assert.That(output).Contains("href=\"#a\"");
        await Assert.That(output).Contains("href=\"#b\"");
        await Assert.That(output).Contains("href=\"#c\"");

        // Entity escapes: the renderer further escapes the source's &amp; into &amp;amp;.
        await Assert.That(output).Contains("&amp;amp;");
        await Assert.That(output).Contains("&amp;gt;");
    }
}
