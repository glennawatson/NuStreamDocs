// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Tests for the <see cref="CitationEntry"/> record.</summary>
public class CitationEntryTests
{
    /// <summary>Publication year in the citation fixture.</summary>
    private const int PublicationYear = 2026;

    /// <summary>Publication month in the citation fixture.</summary>
    private const int PublicationMonth = 5;

    /// <summary>Publication day in the citation fixture.</summary>
    private const int PublicationDay = 2;

    /// <summary>Month and Day properties can be set and retrieved.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MonthAndDayProperties()
    {
        CitationEntry entry = new() { Id = [.. "test"u8], Type = EntryType.Book, Year = PublicationYear, Month = PublicationMonth, Day = PublicationDay };

        await Assert.That(entry.Month).IsEqualTo(PublicationMonth);
        await Assert.That(entry.Day).IsEqualTo(PublicationDay);
    }
}
