// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Fluent builder for assembling a <see cref="BibliographyDatabase"/>.
/// Each <c>Add*</c> shortcut targets a common entry type;
/// <see cref="Add(CitationEntry)"/> is the escape hatch for fields the
/// shortcuts don't cover.
/// </summary>
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
    /// <summary>Entries in insertion order.</summary>
    private readonly List<CitationEntry> _entries = [];

    /// <summary>Adds an arbitrary entry.</summary>
    /// <param name="entry">Entry to add.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder Add(CitationEntry entry)
    {
        _entries.Add(entry);
        return this;
    }

    /// <summary>Adds a book entry from UTF-8 byte arrays.</summary>
    /// <param name="id">Citation key bytes.</param>
    /// <param name="title">Book-title bytes.</param>
    /// <param name="author">Single author.</param>
    /// <param name="year">Publication year.</param>
    /// <param name="publisher">Publisher bytes.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddBook(byte[] id, byte[] title, PersonName author, int year, byte[] publisher)
    {
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

    /// <summary>Adds a journal-article entry from UTF-8 byte arrays.</summary>
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

    /// <summary>Adds a legal-case entry from UTF-8 byte arrays.</summary>
    /// <param name="id">Citation key bytes.</param>
    /// <param name="name">Case-name bytes.</param>
    /// <param name="lawReportSeries">AGLC4 law-report-series citation bytes.</param>
    /// <param name="year">Decision year.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddCase(byte[] id, byte[] name, byte[] lawReportSeries, int year)
    {
        return Add(new()
        {
            Id = id,
            Type = EntryType.LegalCase,
            Title = name,
            Year = year,
            LawReportSeries = lawReportSeries
        });
    }

    /// <summary>Adds a legislation entry from UTF-8 byte arrays.</summary>
    /// <param name="id">Citation key bytes.</param>
    /// <param name="title">Statute-title bytes.</param>
    /// <param name="jurisdiction">Jurisdiction-code bytes.</param>
    /// <param name="year">Year of enactment.</param>
    /// <returns>This builder for chaining.</returns>
    public BibliographyDatabaseBuilder AddLegislation(byte[] id, byte[] title, byte[] jurisdiction, int year)
    {
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
    /// <returns>The built <see cref="BibliographyDatabase"/>.</returns>
    public BibliographyDatabase Build() => new(_entries);
}
