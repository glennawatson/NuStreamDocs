// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Database lookup, fluent builder shortcuts, and uniqueness invariants.</summary>
public class BibliographyDatabaseTests
{
    /// <summary>Publication year of the book fixture.</summary>
    private const int BookYear = 2018;

    /// <summary>Decision year of the Mabo fixture.</summary>
    private const int CaseYear = 1992;

    /// <summary>Year of the legislation fixture.</summary>
    private const int LegislationYear = 1979;

    /// <summary>Publication year of the article and case fixtures.</summary>
    private const int PublicationYear = 2020;

    /// <summary>Expected number of entries in the database.</summary>
    private const int ExpectedEntryCount = 4;

    /// <summary>Publication year of the first book.</summary>
    private const int FirstBookYear = 2000;

    /// <summary>Publication year of the second book.</summary>
    private const int SecondBookYear = 2001;

    /// <summary>Expected number of entries in the ordered snapshot.</summary>
    private const int OrderedEntryCount = 2;

    /// <summary>Builder shortcuts populate the right entry types.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FluentBuilderPopulatesEntries()
    {
        var db = new BibliographyDatabaseBuilder()
            .AddBook(
            [.. "g"u8],
            [.. "Change and Continuity"u8],
            PersonName.Of("William", "Gummow"),
            BookYear,
            [.. "Federation Press"u8]).AddCase([.. "mabo"u8], [.. "Mabo v Queensland (No 2)"u8], [.. "(1992) 175 CLR 1"u8], CaseYear)
            .AddLegislation([.. "hca"u8], [.. "High Court of Australia Act"u8], [.. "Cth"u8], LegislationYear)
            .AddArticle(
            [.. "smith"u8],
            [.. "On Federalism"u8],
            PersonName.Of("Anne", "Smith"),
            PublicationYear,
            [.. "Australian Law Journal"u8],
            [.. "94"u8],
            [.. "200"u8]).Build();

        await Assert.That(db.Count).IsEqualTo(ExpectedEntryCount);
        await Assert.That(db.TryGet("g"u8, out var book)).IsTrue();
        await Assert.That(book!.Type).IsEqualTo(EntryType.Book);
        await Assert.That(db.TryGet("mabo"u8, out var c)).IsTrue();
        await Assert.That(c!.Type).IsEqualTo(EntryType.LegalCase);
        await Assert.That(db.TryGet("hca"u8, out var leg)).IsTrue();
        await Assert.That(leg!.Type).IsEqualTo(EntryType.Legislation);
        await Assert.That(db.TryGet("smith"u8, out var art)).IsTrue();
        await Assert.That(art!.Type).IsEqualTo(EntryType.ArticleJournal);
    }

    /// <summary>Unknown keys miss cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LookupMissesReturnFalse()
    {
        var db = new BibliographyDatabaseBuilder().Build();
        await Assert.That(db.TryGet("nope"u8, out _)).IsFalse();
    }

    /// <summary>Duplicate ids are rejected at construction.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DuplicateIdsThrow()
    {
        var ex = Assert.Throws<ArgumentException>(static () =>
        {
            _ = new BibliographyDatabaseBuilder()
                .AddBook([.. "dup"u8], [.. "A"u8], PersonName.Of("X", "Y"), FirstBookYear, [.. "P"u8])
                .AddBook([.. "dup"u8], [.. "B"u8], PersonName.Of("X", "Y"), SecondBookYear, [.. "P"u8])
                .Build();
        });
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Empty database is the singleton sentinel.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyDatabaseHasZeroCount() =>
        await Assert.That(BibliographyDatabase.Empty.Count).IsEqualTo(0);

    /// <summary>All returns the ordered snapshot.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task All_returns_ordered_snapshot()
    {
        var db = new BibliographyDatabaseBuilder()
            .AddBook([.. "b"u8], [.. "B"u8], PersonName.Of("X", "Y"), FirstBookYear, [.. "P"u8])
            .AddBook([.. "a"u8], [.. "A"u8], PersonName.Of("X", "Y"), SecondBookYear, [.. "P"u8])
            .Build();

        await Assert.That(db.All.Length).IsEqualTo(OrderedEntryCount);

        // BibliographyDatabase preserves insertion order in the _ordered array.
        await Assert.That(db.All[0].Id.AsSpan().SequenceEqual("b"u8)).IsTrue();
        await Assert.That(db.All[1].Id.AsSpan().SequenceEqual("a"u8)).IsTrue();
    }
}
