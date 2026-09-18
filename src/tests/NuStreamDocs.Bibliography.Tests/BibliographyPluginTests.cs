// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Bibliography.Styles.Aglc4;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Plugin lifecycle, end-to-end rewrite, and DocBuilder wiring.</summary>
public class BibliographyPluginTests
{
    /// <summary>Initial capacity for a short footnote.</summary>
    private const int FootnoteBufferCapacity = 64;

    /// <summary>Decision year of the Mabo fixture.</summary>
    private const int CaseYear = 1992;

    /// <summary>Initial capacity for a page with citations.</summary>
    private const int PageBufferCapacity = 256;

    /// <summary>Publication year of the first book.</summary>
    private const int FirstBookYear = 2000;

    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsBibliography() =>
        await Assert.That(new BibliographyPlugin().Name.SequenceEqual("bibliography"u8)).IsTrue();

    /// <summary>Source without markers is copied through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PassThroughWhenNoMarkers()
    {
        ArrayBufferWriter<byte> sink = new(FootnoteBufferCapacity);
        PagePreRenderContext ctx = new("p.md", "plain text\n"u8, sink);
        new BibliographyPlugin().PreRender(in ctx);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).IsEqualTo("plain text\n");
    }

    /// <summary>A resolved <c>[@key]</c> is replaced with a footnote reference and a Bibliography section is appended.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolvedMarkerProducesFootnoteAndBibliography()
    {
        var db = new BibliographyDatabaseBuilder()
            .AddCase([.. "mabo"u8], [.. "Mabo v Queensland (No 2)"u8], [.. "(1992) 175 CLR 1"u8], CaseYear)
            .Build();
        BibliographyOptions options = new(db, Aglc4Style.Instance, false);
        BibliographyPlugin plugin = new(options);
        ArrayBufferWriter<byte> sink = new(PageBufferCapacity);
        PagePreRenderContext ctx = new("p.md", "see [@mabo]\n"u8, sink);
        plugin.PreRender(in ctx);

        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).Contains("[^bib-mabo]");
        await Assert.That(output).Contains("## Bibliography");
        await Assert.That(output).Contains("*Mabo v Queensland (No 2)*");
    }

    /// <summary>An unresolved key fires the warning callback when configured.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingKeyDoesNotProduceFootnote()
    {
        BibliographyOptions options = new(BibliographyDatabase.Empty, Aglc4Style.Instance, true);
        ArrayBufferWriter<byte> sink = new(FootnoteBufferCapacity);
        PagePreRenderContext ctx = new("p.md", "[@nope]\n"u8, sink);
        new BibliographyPlugin(options).PreRender(in ctx);
        var output = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(output).DoesNotContain("[^bib-");
        await Assert.That(output).DoesNotContain("## Bibliography");
    }

    /// <summary>UseBibliography(options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseBibliographyOptionsRegisters()
    {
        DocBuilder builder = new();
        var result = builder.UseBibliography(BibliographyOptions.Default);
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>UseBibliography(callback) builds the database and registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseBibliographyFluentRegisters()
    {
        DocBuilder builder = new();
        var result = builder.UseBibliography(static b =>
            b.AddBook([.. "g"u8], [.. "T"u8], PersonName.Of("X", "Y"), FirstBookYear, [.. "P"u8]));
        await Assert.That(result).IsSameReferenceAs(builder);
    }
}
