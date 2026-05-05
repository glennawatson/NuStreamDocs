// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Abbr.Tests;

/// <summary>Behavior tests for <c>AbbrRewriter</c>.</summary>
public class AbbrRewriterTests
{
    /// <summary>A simple definition is removed and every occurrence wrapped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SimpleDefinitionWrapsOccurrences()
    {
        const string Source = "*[HTML]: HyperText Markup Language\nThe HTML spec is long.";
        const string Expected = "The <abbr title=\"HyperText Markup Language\">HTML</abbr> spec is long.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Expected);
    }

    /// <summary>Multiple definitions all wrap their tokens; longer-first ordering wins on overlap.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultipleDefinitionsWrap()
    {
        const string Source = "*[CSS]: Cascading Style Sheets\n*[HTML]: HyperText Markup Language\nUse CSS with HTML.";
        await Assert.That(Rewrite(Source))
            .IsEqualTo("Use <abbr title=\"Cascading Style Sheets\">CSS</abbr> with <abbr title=\"HyperText Markup Language\">HTML</abbr>.");
    }

    /// <summary>Only word-boundary occurrences are wrapped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubwordOccurrencesAreNotWrapped()
    {
        const string Source = "*[HTML]: HyperText\nXHTMLish stuff and HTML5 too.";
        await Assert.That(Rewrite(Source)).IsEqualTo("XHTMLish stuff and HTML5 too.");
    }

    /// <summary>Occurrences inside fenced-code blocks are not wrapped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeIsHonored()
    {
        const string Source = "*[HTML]: HyperText\nUse HTML.\n```\nHTML inside code\n```\nAfter HTML.";
        await Assert.That(Rewrite(Source))
            .IsEqualTo("Use <abbr title=\"HyperText\">HTML</abbr>.\n```\nHTML inside code\n```\nAfter <abbr title=\"HyperText\">HTML</abbr>.");
    }

    /// <summary>Occurrences inside inline-code spans are not wrapped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodeIsHonored()
    {
        const string Source = "*[HTML]: HyperText\nThe `HTML` literal vs HTML wrapping.";
        await Assert.That(Rewrite(Source))
            .IsEqualTo("The `HTML` literal vs <abbr title=\"HyperText\">HTML</abbr> wrapping.");
    }

    /// <summary>Definition values are HTML-attribute-escaped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefinitionIsHtmlAttributeEscaped()
    {
        const string Source = "*[X]: a \"quoted\" & dangerous <bad>\nThe X here.";
        await Assert.That(Rewrite(Source))
            .IsEqualTo("The <abbr title=\"a &quot;quoted&quot; &amp; dangerous &lt;bad&gt;\">X</abbr> here.");
    }

    /// <summary>Source with no definitions round-trips byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoDefinitionsRoundTrips()
    {
        const string Source = "Plain text, no abbreviations.";
        await Assert.That(Rewrite(Source)).IsEqualTo(Source);
    }

    /// <summary>Definition lines are stripped even when no occurrences exist.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefinitionStrippedEvenWhenUnused()
    {
        const string Source = "*[XYZ]: Unused\nNothing references it.";
        await Assert.That(Rewrite(Source)).IsEqualTo("Nothing references it.");
    }

    /// <summary>Empty input yields empty output.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyInputEmptyOutput() => await Assert.That(Rewrite(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>Rewrites <paramref name="input"/> via <c>AbbrRewriter</c>.</summary>
    /// <param name="input">Markdown source.</param>
    /// <returns>Rewritten text.</returns>
    private static string Rewrite(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        ArrayBufferWriter<byte> sink = new(Math.Max(bytes.Length, 1));
        AbbrRewriter.Rewrite(bytes, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
