// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Bibliography.Styles.Aglc4;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>End-to-end shape tests for AGLC4 byte-writing formatters.</summary>
public class Aglc4StyleTests
{
    /// <summary>Books emit <c>Author, *Title* (Publisher, Year)</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BookRendersAglc4Form()
    {
        CitationEntry entry = new()
        {
            Id = [.. "x"u8],
            Type = EntryType.Book,
            Title = [.. "Change and Continuity"u8],
            Authors = [PersonName.Of("William", "Gummow")],
            Year = 2018,
            Publisher = [.. "Federation Press"u8]
        };
        await Assert.That(RenderBibliography(entry))
            .IsEqualTo("William Gummow, *Change and Continuity* (Federation Press, 2018)");
    }

    /// <summary>Cases italicize the case name and append the law-report series.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CaseRendersWithSeries()
    {
        CitationEntry entry = new()
        {
            Id = [.. "mabo"u8],
            Type = EntryType.LegalCase,
            Title = [.. "Mabo v Queensland (No 2)"u8],
            Year = 1992,
            LawReportSeries = [.. "(1992) 175 CLR 1"u8]
        };
        await Assert.That(RenderBibliography(entry)).IsEqualTo("*Mabo v Queensland (No 2)* (1992) 175 CLR 1");
    }

    /// <summary>Articles wrap title in single quotes and italicize the journal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ArticleRendersWithSingleQuotedTitle()
    {
        CitationEntry entry = new()
        {
            Id = [.. "smith"u8],
            Type = EntryType.ArticleJournal,
            Title = [.. "On Federalism"u8],
            Authors = [PersonName.Of("Anne", "Smith")],
            Year = 2020,
            ContainerTitle = [.. "Australian Law Journal"u8],
            Volume = [.. "94"u8],
            Page = [.. "200"u8]
        };
        var output = RenderBibliography(entry);
        await Assert.That(output).Contains("'On Federalism'");
        await Assert.That(output).Contains("*Australian Law Journal*");
        await Assert.That(output).Contains("94");
        await Assert.That(output).Contains("200");
    }

    /// <summary>Legislation italicizes the title and includes jurisdiction.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LegislationRendersAglc4Form()
    {
        CitationEntry entry = new()
        {
            Id = [.. "hca-act"u8],
            Type = EntryType.Legislation,
            Title = [.. "High Court of Australia Act 1979"u8],
            Jurisdiction = [.. "Cth"u8],
            Year = 1979
        };
        var output = RenderBibliography(entry);
        await Assert.That(output).IsEqualTo("*High Court of Australia Act 1979* (Cth)");
    }

    /// <summary>Pinpoint locator on a footnote emits the AGLC4 prefix.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FootnoteAppendsLocator()
    {
        CitationEntry entry = new() { Id = [.. "x"u8], Type = EntryType.Book, Title = [.. "T"u8] };
        byte[] source = [.. "23"u8];
        CitationLocator locator = new(LocatorKind.Page, 0, source.Length);
        ArrayBufferWriter<byte> sink = new(64);
        Aglc4Style.Instance.WriteFootnote(entry, locator, source, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).EndsWith(" 23");
    }

    /// <summary>Paragraph locator wraps the value in square brackets.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FootnoteParagraphLocatorIsBracketed()
    {
        CitationEntry entry = new() { Id = [.. "x"u8], Type = EntryType.Book, Title = [.. "T"u8] };
        byte[] source = [.. "12"u8];
        CitationLocator locator = new(LocatorKind.Paragraph, 0, source.Length);
        ArrayBufferWriter<byte> sink = new(64);
        Aglc4Style.Instance.WriteFootnote(entry, locator, source, sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).EndsWith(" [12]");
    }

    /// <summary>Style name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StyleNameIsAglc4() =>
        await Assert.That(Aglc4Style.Instance.Name.SequenceEqual("AGLC4"u8)).IsTrue();

    /// <summary>Renders <paramref name="entry"/> via <see cref="Aglc4Style"/>'s bibliography path and decodes the UTF-8 result.</summary>
    /// <param name="entry">Resolved entry.</param>
    /// <returns>Decoded markdown line.</returns>
    private static string RenderBibliography(CitationEntry entry)
    {
        ArrayBufferWriter<byte> sink = new(128);
        Aglc4Style.Instance.WriteBibliography(entry, sink);
        return Encoding.UTF8.GetString(sink.WrittenSpan);
    }
}
