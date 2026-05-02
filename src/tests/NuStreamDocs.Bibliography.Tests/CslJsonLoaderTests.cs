// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Csl;
using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>CSL-JSON parser — round-trips canonical CSL fields into <see cref="CitationEntry"/>.</summary>
public class CslJsonLoaderTests
{
    /// <summary>A book entry round-trips through the loader.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BookEntryParses()
    {
        var json = """
            [{"id":"g","type":"book","title":"Change and Continuity","author":[{"family":"Gummow","given":"William"}],"issued":{"date-parts":[[2018]]},"publisher":"Federation Press"}]
            """u8;
        var entries = CslJsonLoader.Parse(json.ToArray());
        await Assert.That(entries).HasSingleItem();
        var e = entries[0];
        await Assert.That(e.Id).IsEqualTo("g");
        await Assert.That(e.Type).IsEqualTo(EntryType.Book);
        await Assert.That(e.Title).IsEqualTo("Change and Continuity");
        await Assert.That(e.Year).IsEqualTo(2018);
        await Assert.That(e.Authors).HasSingleItem();
        await Assert.That(e.Authors[0].Family).IsEqualTo("Gummow");
    }

    /// <summary>A legal case entry maps the CSL <c>legal_case</c> + <c>references</c> fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LegalCaseEntryParses()
    {
        var json = """
            [{"id":"mabo","type":"legal_case","title":"Mabo v Queensland (No 2)","references":"(1992) 175 CLR 1","issued":{"date-parts":[[1992]]}}]
            """u8;
        var entries = CslJsonLoader.Parse(json.ToArray());
        await Assert.That(entries[0].Type).IsEqualTo(EntryType.LegalCase);
        await Assert.That(entries[0].LawReportSeries).IsEqualTo("(1992) 175 CLR 1");
    }

    /// <summary>Entries without an id are dropped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingIdIsDropped()
    {
        var json = """[{"type":"book","title":"No Id"}]"""u8;
        var entries = CslJsonLoader.Parse(json.ToArray());
        await Assert.That(entries).IsEmpty();
    }

    /// <summary>An empty array yields an empty list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyArrayYieldsEmpty()
    {
        var entries = CslJsonLoader.Parse("[]"u8.ToArray());
        await Assert.That(entries).IsEmpty();
    }

    /// <summary>An unknown CSL type falls through to <see cref="EntryType.Other"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnknownTypeBecomesOther()
    {
        var json = """[{"id":"x","type":"some-future-type","title":"T"}]"""u8;
        var entries = CslJsonLoader.Parse(json.ToArray());
        await Assert.That(entries[0].Type).IsEqualTo(EntryType.Other);
    }

    /// <summary>LoadFile reads from disk.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoadFile_reads_from_disk()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """[{"id":"f","type":"book","title":"File"}]""");
            var entries = CslJsonLoader.LoadFile(path);
            await Assert.That(entries).HasSingleItem();
            await Assert.That(entries[0].Id).IsEqualTo("f");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
