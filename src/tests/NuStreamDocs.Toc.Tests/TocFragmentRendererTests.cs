// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Toc.Tests;

/// <summary>Direct tests for the TocFragmentRenderer covering nesting and entity-escape branches.</summary>
public class TocFragmentRendererTests
{
    /// <summary>Link target for the child heading shared by nesting fixtures.</summary>
    private const string ChildLink = "href=\"#b\"";

    /// <summary>Opening list tag used to count nesting levels.</summary>
    private const string ListOpen = "<ul";

    /// <summary>Closing list tag used to verify balanced nesting.</summary>
    private const string ListClose = "</ul>";

    /// <summary>Empty heading list emits nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyEmitsNothing()
    {
        ArrayBufferWriter<byte> sink = new();
        TocFragmentRenderer.Render([], [], TocOptions.Default, sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Headings outside the level filter emit nothing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FilteredOut()
    {
        const int minimumLevel = 2;
        const int maximumLevel = 6;
        byte[] html = [.. "<h1>x</h1>"u8];
        var headings = HeadingScanner.Scan(html);
        ArrayBufferWriter<byte> sink = new();
        TocFragmentRenderer.Render(html, headings, new(minimumLevel, maximumLevel, "#", false), sink);
        await Assert.That(sink.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Nested headings produce nested ul/li with entity escapes for special chars.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NestedAndEscaped()
    {
        byte[] html = [.. "<h2 id=\"a\">A &amp; B &gt; C</h2><h3 id=\"b\">child</h3><h2 id=\"c\">D</h2>"u8];
        var headings = HeadingScanner.Scan(html);
        var slugged = AssignExistingIds(html, headings);
        ArrayBufferWriter<byte> sink = new();
        TocFragmentRenderer.Render(html, slugged, TocOptions.Default, sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("<nav class=\"md-nav md-nav--secondary\"");
        await Assert.That(output).Contains("aria-label=\"On this page\"");
        await Assert.That(output).Contains("md-nav__list");
        await Assert.That(output).Contains("href=\"#a\"");
        await Assert.That(output).Contains(ChildLink);
        await Assert.That(output).Contains("href=\"#c\"");

        // Entity escapes: the renderer further escapes the source's &amp; into &amp;amp;.
        await Assert.That(output).Contains("&amp;amp;");
        await Assert.That(output).Contains("&amp;gt;");
    }

    /// <summary>Multiple consecutive same-level headings render as siblings, not as ever-deepening nesting.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SameLevelSiblingsStayFlat()
    {
        const int listCount = 2;
        byte[] html = [.. "<h2 id=\"top\">T</h2><h3 id=\"a\">A</h3><h3 id=\"b\">B</h3><h3 id=\"c\">C</h3>"u8];
        var headings = HeadingScanner.Scan(html);
        var slugged = AssignExistingIds(html, headings);
        ArrayBufferWriter<byte> sink = new();
        TocFragmentRenderer.Render(html, slugged, TocOptions.Default, sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);

        // Outer + one child <ul> for the H3 group; nothing deeper.
        await Assert.That(CountSubstring(output, ListOpen)).IsEqualTo(listCount);
        await Assert.That(CountSubstring(output, ListClose)).IsEqualTo(listCount);
        await Assert.That(output).Contains("href=\"#a\"");
        await Assert.That(output).Contains(ChildLink);
        await Assert.That(output).Contains("href=\"#c\"");
    }

    /// <summary>Mixed depth (H2/H3/H4/H3/H2) closes nested levels and resumes parent siblings cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MixedDepthClosesAndReopensCleanly()
    {
        const int listCount = 3;
        byte[] html =
        [
            .. "<h2 id=\"s1\">S1</h2><h3 id=\"a\">A</h3><h4 id=\"a1\">A1</h4><h3 id=\"b\">B</h3><h2 id=\"s2\">S2</h2>"u8
        ];
        var headings = HeadingScanner.Scan(html);
        var slugged = AssignExistingIds(html, headings);
        ArrayBufferWriter<byte> sink = new();
        TocFragmentRenderer.Render(html, slugged, TocOptions.Default, sink);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);

        // Outer + H3 group + H4 group = 3 ul opens / 3 ul closes.
        await Assert.That(CountSubstring(output, ListOpen)).IsEqualTo(listCount);
        await Assert.That(CountSubstring(output, ListClose)).IsEqualTo(listCount);
        await Assert.That(output).Contains("href=\"#s1\"");
        await Assert.That(output).Contains("href=\"#a1\"");
        await Assert.That(output).Contains(ChildLink);
        await Assert.That(output).Contains("href=\"#s2\"");
    }

    /// <summary>Uses each heading's explicit id as its slug.</summary>
    /// <param name="html">HTML containing the heading ids.</param>
    /// <param name="headings">Headings to prepare for rendering.</param>
    /// <returns>Headings with assigned slugs.</returns>
    private static Heading[] AssignExistingIds(ReadOnlySpan<byte> html, Heading[] headings)
    {
        var slugged = new Heading[headings.Length];
        for (var i = 0; i < headings.Length; i++)
        {
            slugged[i] = headings[i] with { Slug = [.. headings[i].ExistingIdBytes(html)] };
        }

        return slugged;
    }

    /// <summary>Counts non-overlapping occurrences of <paramref name="needle"/> in <paramref name="haystack"/>.</summary>
    /// <param name="haystack">String to search.</param>
    /// <param name="needle">Substring to count.</param>
    /// <returns>Match count.</returns>
    private static int CountSubstring(string haystack, string needle)
    {
        var count = 0;
        for (var idx = 0; (idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0; idx += needle.Length)
        {
            count++;
        }

        return count;
    }
}
