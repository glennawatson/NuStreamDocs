// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Toc.Tests;

/// <summary>Tests for <c>HeadingRewriter</c>.</summary>
public class HeadingRewriterTests
{
    /// <summary>Rewriter inserts id attributes and permalink anchors.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EmitsIdAndPermalinkAnchor()
    {
        var html = "<h2>Hello World</h2>"u8.ToArray();
        var headings = HeadingScanner.Scan(html);
        var (slugged, _) = HeadingSlugifier.AssignSlugs(html, headings);

        var sink = new ArrayBufferWriter<byte>(128);
        HeadingRewriter.Rewrite(html, slugged, "¶", sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(output).Contains("<h2 id=\"hello-world\">");
        await Assert.That(output).Contains("<a class=\"headerlink\" href=\"#hello-world\" title=\"Permanent link\">¶</a>");
        await Assert.That(output).Contains("</h2>");
    }

    /// <summary>Existing id is not duplicated.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreservesExistingId()
    {
        var html = "<h2 id=\"keep\">Body</h2>"u8.ToArray();
        var headings = HeadingScanner.Scan(html);
        var (slugged, _) = HeadingSlugifier.AssignSlugs(html, headings);

        var sink = new ArrayBufferWriter<byte>(128);
        HeadingRewriter.Rewrite(html, slugged, "#", sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(output).Contains("<h2 id=\"keep\">");
        await Assert.That(output).Contains("href=\"#keep\"");

        // Should not contain a duplicate id attribute.
        var firstIdx = output.IndexOf("id=", StringComparison.Ordinal);
        var secondIdx = output.IndexOf("id=", firstIdx + 1, StringComparison.Ordinal);
        await Assert.That(secondIdx).IsEqualTo(-1);
    }

    /// <summary>Non-heading content is preserved verbatim.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PreservesSurroundingContent()
    {
        var html = "<p>before</p><h2>Title</h2><p>after</p>"u8.ToArray();
        var headings = HeadingScanner.Scan(html);
        var (slugged, _) = HeadingSlugifier.AssignSlugs(html, headings);

        var sink = new ArrayBufferWriter<byte>(128);
        HeadingRewriter.Rewrite(html, slugged, "¶", sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);

        await Assert.That(output).StartsWith("<p>before</p>");
        await Assert.That(output).EndsWith("<p>after</p>");
    }
}
