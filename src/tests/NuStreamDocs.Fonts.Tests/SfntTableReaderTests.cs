// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="SfntTableReader"/>.</summary>
public class SfntTableReaderTests
{
    /// <summary>Reads back the metrics written into a stub sfnt's head/hhea/OS-2 tables.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsStubSfntMetrics()
    {
        var sfnt = StubFont.BuildSfnt(unitsPerEm: 1000, ascender: 950, descender: -250, lineGap: 0, xHeight: 500, capHeight: 700);
        var m = SfntTableReader.TryRead(sfnt);
        await Assert.That(m.HasValue).IsTrue();
        await Assert.That(m!.Value.UnitsPerEm).IsEqualTo(1000);
        await Assert.That(m.Value.Ascender).IsEqualTo(950);
        await Assert.That(m.Value.Descender).IsEqualTo(-250);
        await Assert.That(m.Value.LineGap).IsEqualTo(0);
        await Assert.That(m.Value.XHeight).IsEqualTo(500);
        await Assert.That(m.Value.CapHeight).IsEqualTo(700);
    }

    /// <summary>Garbage bytes yield <see langword="null"/> rather than throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GarbageReturnsNull()
    {
        await Assert.That(SfntTableReader.TryRead([1, 2, 3, 4, 5])).IsNull();
        await Assert.That(SfntTableReader.TryRead([])).IsNull();
    }
}
