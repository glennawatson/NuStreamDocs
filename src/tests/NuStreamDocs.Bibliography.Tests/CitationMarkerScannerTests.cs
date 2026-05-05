// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Bibliography.Styles.Aglc4;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Pandoc-style citation marker grammar — single, multi, locator-bearing, escape-into-code — verified end-to-end through the rewriter so the single-pass walker stays honest.</summary>
public class CitationMarkerScannerTests
{
    /// <summary>A bare <c>[@key]</c> marker resolves into a footnote reference and emits a Bibliography section with a single entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleKeyIsRecognized()
    {
        var output = Render("see [@mabo] for context");
        await Assert.That(output).Contains("[^bib-mabo]");
        await Assert.That(output).Contains("## Bibliography");
    }

    /// <summary>A locator label and value are split correctly and the AGLC4 prefix appears in the footnote definition.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LocatorWithLabelIsClassified()
    {
        var output = Render("[@mabo, p 23]");
        await Assert.That(output).Contains("[^bib-mabo]:");
        await Assert.That(output).Contains(" 23");
    }

    /// <summary>Multi-cite <c>[@a; @b]</c> emits two footnote references separated by <c>"; "</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MultiCiteSplitsOnSemicolon()
    {
        var output = Render("[@one; @two]");
        await Assert.That(output).Contains("[^bib-one]");
        await Assert.That(output).Contains("[^bib-two]");
        await Assert.That(output).Contains("; ");
    }

    /// <summary>Markers inside fenced code blocks are skipped — only the outside marker resolves.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FencedCodeIsSkipped()
    {
        var output = Render("```\n[@nope]\n```\n[@yes]\n");
        await Assert.That(output).Contains("[@nope]");
        await Assert.That(output).Contains("[^bib-yes]");
    }

    /// <summary>Markers inside inline code spans are skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineCodeIsSkipped()
    {
        var output = Render("`[@inline]` and [@real]");
        await Assert.That(output).Contains("`[@inline]`");
        await Assert.That(output).Contains("[^bib-real]");
    }

    /// <summary>Locator value with a hyphen range is preserved in the footnote definition.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LocatorRangeIsPreserved()
    {
        var output = Render("[@mabo, pp 23-25]");
        await Assert.That(output).Contains("23-25");
    }

    /// <summary>An unrecognized label round-trips through <see cref="LocatorKind.Other"/> — both label and value land verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownLabelIsOther()
    {
        var output = Render("[@mabo, foo 9]");
        await Assert.That(output).Contains("foo 9");
    }

    /// <summary>An empty source produces no markers and no Bibliography section.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptySourceProducesNothing()
    {
        var output = Render(string.Empty);
        await Assert.That(output).IsEqualTo(string.Empty);
        await Assert.That(output).DoesNotContain("Bibliography");
    }

    /// <summary>Renders <paramref name="markdown"/> through the bibliography plugin against a fixed three-entry database and returns the UTF-8 result as a string.</summary>
    /// <param name="markdown">Source markdown.</param>
    /// <returns>Rendered output.</returns>
    private static string Render(string markdown)
    {
        var db = new BibliographyDatabaseBuilder()
            .AddCase("mabo", "Mabo v Queensland (No 2)", "(1992) 175 CLR 1", 1992)
            .AddCase("one", "Case One", "[2020] 1", 2020)
            .AddCase("two", "Case Two", "[2020] 2", 2020)
            .AddCase("yes", "Case Yes", "[2020] 3", 2020)
            .AddCase("real", "Case Real", "[2020] 4", 2020)
            .Build();
        BibliographyOptions options = new(db, Aglc4Style.Instance, WarnOnMissing: false);
        BibliographyPlugin plugin = new(options);
        ArrayBufferWriter<byte> sink = new(Math.Max(markdown.Length * 4, 16));
        PagePreRenderContext ctx = new("p.md", Encoding.UTF8.GetBytes(markdown), sink);
        plugin.PreRender(in ctx);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
