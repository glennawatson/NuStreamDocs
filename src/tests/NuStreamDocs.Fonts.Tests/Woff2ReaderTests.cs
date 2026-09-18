// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="Woff2Reader"/>.</summary>
public class Woff2ReaderTests
{
    /// <summary>Expected units per em in the fixture.</summary>
    private const int UnitsPerEm = 2048;

    /// <summary>Expected ascender in the fixture.</summary>
    private const int Ascender = 1900;

    /// <summary>Expected descender in the fixture.</summary>
    private const int Descender = -500;

    /// <summary>Height of the fixture's lowercase x.</summary>
    private const int XHeight = 1082;

    /// <summary>Expected cap height in the fixture.</summary>
    private const int CapHeight = 1462;

    /// <summary>A woff2 wrapping the same tables yields the same metrics as the equivalent sfnt.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsStubWoff2MetricsMatchingSfnt()
    {
        var woff2 = StubFont.BuildWoff2(UnitsPerEm, Ascender, Descender, 0, XHeight, CapHeight);
        var fromWoff2 = Woff2Reader.TryRead(woff2);
        var fromSfnt = SfntTableReader.TryRead(StubFont.BuildSfnt(UnitsPerEm, Ascender, Descender, 0, XHeight, CapHeight));
        await Assert.That(fromWoff2.HasValue).IsTrue();
        await Assert.That(fromWoff2).IsEqualTo(fromSfnt);
        await Assert.That(fromWoff2!.Value.UnitsPerEm).IsEqualTo(UnitsPerEm);
        await Assert.That(fromWoff2.Value.Ascender).IsEqualTo(Ascender);
        await Assert.That(fromWoff2.Value.CapHeight).IsEqualTo(CapHeight);
    }

    /// <summary>Garbage bytes yield <see langword="null"/> rather than throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GarbageReturnsNull()
    {
        await Assert.That(Woff2Reader.TryRead("\u0001\u0002\u0003\u0004\u0005\u0006\u0007\u0008"u8)).IsNull();
        await Assert.That(Woff2Reader.TryRead([])).IsNull();
    }
}
