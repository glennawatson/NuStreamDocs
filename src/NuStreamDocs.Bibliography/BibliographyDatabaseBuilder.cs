// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Fluent builder for assembling a <see cref="BibliographyDatabase"/>
/// directly in <c>Program.cs</c>. Each <c>Add*</c> shortcut targets a
/// common entry type; <see cref="Add(CitationEntry)"/> is the escape
/// hatch for fields the shortcuts don't cover.
/// </summary>
/// <remarks>
/// Each typed shortcut has both a <see cref="string"/> overload (encodes once at construction)
/// and a <see cref="byte"/>[] overload for callers that already hold UTF-8 byte arrays
/// (CSL loader, snapshot replay).
/// </remarks>
/// <example>
/// <code>
/// var db = new BibliographyDatabaseBuilder()
///     .AddBook("gummow-2018", title: "Change and Continuity",
///         author: PersonName.Of("William", "Gummow"), year: 2018,
///         publisher: "Federation Press")
///     .AddCase("mabo", name: "Mabo v Queensland (No 2)",
///         lawReportSeries: "(1992) 175 CLR 1", year: 1992)
///     .Build();
/// </code>
/// </example>
public sealed class BibliographyDatabaseBuilder
{
    /// <summary>Accumulator; entries are kept in insertion order.</summary>
    private readonly List<CitationEntry> _entries = [];

    /// <summary>Adds an arbitrary entry; the escape hatch when the typed shortcuts don't cover the use case.</summary>
    /// <param name="entry">Entry to add.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder Add(CitationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _entries.Add(entry);
        return this;
    }

    /// <summary>Adds a book entry from C# strings (encoded once to UTF-8).</summary>
    /// <param name="id">Citation key.</param>
    /// <param name="title">Book title.</param>
    /// <param name="author">Single author; use the <see cref="CitationEntry"/> overload for multiple authors.</param>
    /// <param name="year">Publication year.</param>
    /// <param name="publisher">Publisher.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddBook(string id, string title, PersonName author, int year, string publisher) =>
        AddBook(Utf8Encoder.Encode(id), Utf8Encoder.Encode(title), author, year, Utf8Encoder.Encode(publisher));

    /// <summary>Adds a book entry from already-encoded UTF-8 byte arrays.</summary>
    /// <param name="id">Citation key bytes.</param>
    /// <param name="title">Book-title bytes.</param>
    /// <param name="author">Single author.</param>
    /// <param name="year">Publication year.</param>
    /// <param name="publisher">Publisher bytes.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddBook(byte[] id, byte[] title, PersonName author, int year, byte[] publisher)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(publisher);
        return Add(new()
        {
            Id = id,
            Type = EntryType.Book,
            Title = title,
            Authors = [author],
            Year = year,
            Publisher = publisher
        });
    }

    /// <summary>Adds a journal-article entry from C# strings (encoded once to UTF-8).</summary>
    /// <param name="id">Citation key.</param>
    /// <param name="title">Article title.</param>
    /// <param name="author">Single author.</param>
    /// <param name="year">Publication year.</param>
    /// <param name="journal">Journal / container title.</param>
    /// <param name="volume">Volume.</param>
    /// <param name="page">Starting page or page range.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddArticle(string id, string title, PersonName author, int year, string journal, string volume, string page) =>
        AddArticle(Utf8Encoder.Encode(id), Utf8Encoder.Encode(title), author, year, Utf8Encoder.Encode(journal), Utf8Encoder.Encode(volume), Utf8Encoder.Encode(page));

    /// <summary>Adds a journal-article entry from already-encoded UTF-8 byte arrays.</summary>
    /// <param name="id">Citation key bytes.</param>
    /// <param name="title">Article-title bytes.</param>
    /// <param name="author">Single author.</param>
    /// <param name="year">Publication year.</param>
    /// <param name="journal">Journal / container-title bytes.</param>
    /// <param name="volume">Volume bytes.</param>
    /// <param name="page">Page-range bytes.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddArticle(byte[] id, byte[] title, PersonName author, int year, byte[] journal, byte[] volume, byte[] page)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(author);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(volume);
        ArgumentNullException.ThrowIfNull(page);
        return Add(new()
        {
            Id = id,
            Type = EntryType.ArticleJournal,
            Title = title,
            Authors = [author],
            Year = year,
            ContainerTitle = journal,
            Volume = volume,
            Page = page
        });
    }

    /// <summary>Adds a legal-case entry from C# strings (encoded once to UTF-8).</summary>
    /// <param name="id">Citation key.</param>
    /// <param name="name">Case name (e.g. <c>"Mabo v Queensland (No 2)"</c>).</param>
    /// <param name="lawReportSeries">AGLC4 law-report-series citation (e.g. <c>"(1992) 175 CLR 1"</c>).</param>
    /// <param name="year">Decision year.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddCase(string id, string name, string lawReportSeries, int year) =>
        AddCase(Utf8Encoder.Encode(id), Utf8Encoder.Encode(name), Utf8Encoder.Encode(lawReportSeries), year);

    /// <summary>Adds a legal-case entry from already-encoded UTF-8 byte arrays.</summary>
    /// <param name="id">Citation key bytes.</param>
    /// <param name="name">Case-name bytes.</param>
    /// <param name="lawReportSeries">AGLC4 law-report-series citation bytes.</param>
    /// <param name="year">Decision year.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddCase(byte[] id, byte[] name, byte[] lawReportSeries, int year)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(lawReportSeries);
        return Add(new()
        {
            Id = id,
            Type = EntryType.LegalCase,
            Title = name,
            Year = year,
            LawReportSeries = lawReportSeries
        });
    }

    /// <summary>Adds a legislation entry from C# strings (encoded once to UTF-8).</summary>
    /// <param name="id">Citation key.</param>
    /// <param name="title">Statute title (e.g. <c>"High Court of Australia Act 1979"</c>).</param>
    /// <param name="jurisdiction">Jurisdiction code (e.g. <c>"Cth"</c>, <c>"NSW"</c>).</param>
    /// <param name="year">Year of enactment.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddLegislation(string id, string title, string jurisdiction, int year) =>
        AddLegislation(Utf8Encoder.Encode(id), Utf8Encoder.Encode(title), Utf8Encoder.Encode(jurisdiction), year);

    /// <summary>Adds a legislation entry from already-encoded UTF-8 byte arrays.</summary>
    /// <param name="id">Citation key bytes.</param>
    /// <param name="title">Statute-title bytes.</param>
    /// <param name="jurisdiction">Jurisdiction-code bytes.</param>
    /// <param name="year">Year of enactment.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddLegislation(byte[] id, byte[] title, byte[] jurisdiction, int year)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(jurisdiction);
        return Add(new()
        {
            Id = id,
            Type = EntryType.Legislation,
            Title = title,
            Jurisdiction = jurisdiction,
            Year = year
        });
    }

    /// <summary>Builds the immutable database.</summary>
    /// <returns>The frozen <see cref="BibliographyDatabase"/>.</returns>
    public BibliographyDatabase Build() => new(_entries);
}
