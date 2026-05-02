// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography.Tests;

/// <summary>Tests for the <see cref="CitationEntry"/> record.</summary>
public class CitationEntryTests
{
    /// <summary>Month and Day properties can be set and retrieved.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MonthAndDayProperties()
    {
        var entry = new CitationEntry
        {
            Id = "test",
            Type = EntryType.Book,
            Year = 2026,
            Month = 5,
            Day = 2
        };

        await Assert.That(entry.Month).IsEqualTo(5);
        await Assert.That(entry.Day).IsEqualTo(2);
    }
}
