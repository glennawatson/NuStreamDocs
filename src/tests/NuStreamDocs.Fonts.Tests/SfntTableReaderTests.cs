// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="SfntTableReader"/>.</summary>
public class SfntTableReaderTests
{
    /// <summary>Expected units per em in the fixture.</summary>
    private const int UnitsPerEm = 1000;

    /// <summary>Expected ascender in the fixture.</summary>
    private const int Ascender = 950;

    /// <summary>Expected descender in the fixture.</summary>
    private const int Descender = -250;

    /// <summary>Height of the fixture's lowercase x.</summary>
    private const int XHeight = 500;

    /// <summary>Expected cap height in the fixture.</summary>
    private const int CapHeight = 700;

    /// <summary>Reads back the metrics written into a stub sfnt's head/hhea/OS-2 tables.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsStubSfntMetrics()
    {
        var sfnt = StubFont.BuildSfnt(UnitsPerEm, Ascender, Descender, 0, XHeight, CapHeight);
        var m = SfntTableReader.TryRead(sfnt);
        await Assert.That(m.HasValue).IsTrue();
        await Assert.That(m!.Value.UnitsPerEm).IsEqualTo(UnitsPerEm);
        await Assert.That(m.Value.Ascender).IsEqualTo(Ascender);
        await Assert.That(m.Value.Descender).IsEqualTo(Descender);
        await Assert.That(m.Value.LineGap).IsEqualTo(0);
        await Assert.That(m.Value.XHeight).IsEqualTo(XHeight);
        await Assert.That(m.Value.CapHeight).IsEqualTo(CapHeight);
    }

    /// <summary>Garbage bytes yield <see langword="null"/> rather than throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GarbageReturnsNull()
    {
        await Assert.That(SfntTableReader.TryRead("\u0001\u0002\u0003\u0004\u0005"u8)).IsNull();
        await Assert.That(SfntTableReader.TryRead([])).IsNull();
    }
}
