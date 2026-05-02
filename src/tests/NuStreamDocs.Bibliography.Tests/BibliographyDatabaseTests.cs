// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Database lookup, fluent builder shortcuts, and uniqueness invariants.</summary>
public class BibliographyDatabaseTests
{
    /// <summary>Builder shortcuts populate the right entry types.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FluentBuilderPopulatesEntries()
    {
        var db = new BibliographyDatabaseBuilder()
            .AddBook("g", "Change and Continuity", PersonName.Of("William", "Gummow"), 2018, "Federation Press")
            .AddCase("mabo", "Mabo v Queensland (No 2)", "(1992) 175 CLR 1", 1992)
            .AddLegislation("hca", "High Court of Australia Act", "Cth", 1979)
            .AddArticle("smith", "On Federalism", PersonName.Of("Anne", "Smith"), 2020, "Australian Law Journal", "94", "200")
            .Build();

        await Assert.That(db.Count).IsEqualTo(4);
        await Assert.That(db.TryGet("g", out var book)).IsTrue();
        await Assert.That(book!.Type).IsEqualTo(EntryType.Book);
        await Assert.That(db.TryGet("mabo", out var c)).IsTrue();
        await Assert.That(c!.Type).IsEqualTo(EntryType.LegalCase);
        await Assert.That(db.TryGet("hca", out var leg)).IsTrue();
        await Assert.That(leg!.Type).IsEqualTo(EntryType.Legislation);
        await Assert.That(db.TryGet("smith", out var art)).IsTrue();
        await Assert.That(art!.Type).IsEqualTo(EntryType.ArticleJournal);
    }

    /// <summary>Unknown keys miss cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LookupMissesReturnFalse()
    {
        var db = new BibliographyDatabaseBuilder().Build();
        await Assert.That(db.TryGet("nope", out _)).IsFalse();
    }

    /// <summary>Duplicate ids are rejected at construction.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DuplicateIdsThrow()
    {
        var ex = Assert.Throws<ArgumentException>(static () =>
        {
            _ = new BibliographyDatabaseBuilder()
                .AddBook("dup", "A", PersonName.Of("X", "Y"), 2000, "P")
                .AddBook("dup", "B", PersonName.Of("X", "Y"), 2001, "P")
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
            .AddBook("b", "B", PersonName.Of("X", "Y"), 2000, "P")
            .AddBook("a", "A", PersonName.Of("X", "Y"), 2001, "P")
            .Build();

        await Assert.That(db.All.Length).IsEqualTo(2);

        // BibliographyDatabase preserves insertion order in the _ordered array.
        await Assert.That(db.All[0].Id.AsSpan().SequenceEqual("b"u8)).IsTrue();
        await Assert.That(db.All[1].Id.AsSpan().SequenceEqual("a"u8)).IsTrue();
    }
}
