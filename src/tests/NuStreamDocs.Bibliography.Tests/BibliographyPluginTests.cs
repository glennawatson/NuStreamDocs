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
        ArrayBufferWriter<byte> sink = new(64);
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
            .AddCase("mabo", "Mabo v Queensland (No 2)", "(1992) 175 CLR 1", 1992)
            .Build();
        BibliographyOptions options = new(db, Aglc4Style.Instance, WarnOnMissing: false);
        BibliographyPlugin plugin = new(options);
        ArrayBufferWriter<byte> sink = new(256);
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
        BibliographyOptions options = new(BibliographyDatabase.Empty, Aglc4Style.Instance, WarnOnMissing: true);
        ArrayBufferWriter<byte> sink = new(64);
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
            b.AddBook("g", "T", PersonName.Of("X", "Y"), 2000, "P"));
        await Assert.That(result).IsSameReferenceAs(builder);
    }

    /// <summary>UseBibliography rejects a null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseBibliographyRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () =>
            DocBuilderBibliographyExtensions.UseBibliography(null!, BibliographyOptions.Default));
        await Assert.That(ex).IsNotNull();
    }
}
