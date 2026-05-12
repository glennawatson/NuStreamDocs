// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="Woff2Reader"/>.</summary>
public class Woff2ReaderTests
{
    /// <summary>A woff2 wrapping the same tables yields the same metrics as the equivalent sfnt.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadsStubWoff2MetricsMatchingSfnt()
    {
        var woff2 = StubFont.BuildWoff2(unitsPerEm: 2048, ascender: 1900, descender: -500, lineGap: 0, xHeight: 1082, capHeight: 1462);
        var fromWoff2 = Woff2Reader.TryRead(woff2);
        var fromSfnt = SfntTableReader.TryRead(StubFont.BuildSfnt(2048, 1900, -500, 0, 1082, 1462));
        await Assert.That(fromWoff2.HasValue).IsTrue();
        await Assert.That(fromWoff2).IsEqualTo(fromSfnt);
        await Assert.That(fromWoff2!.Value.UnitsPerEm).IsEqualTo(2048);
        await Assert.That(fromWoff2.Value.Ascender).IsEqualTo(1900);
        await Assert.That(fromWoff2.Value.CapHeight).IsEqualTo(1462);
    }

    /// <summary>Garbage bytes yield <see langword="null"/> rather than throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GarbageReturnsNull()
    {
        await Assert.That(Woff2Reader.TryRead([1, 2, 3, 4, 5, 6, 7, 8])).IsNull();
        await Assert.That(Woff2Reader.TryRead([])).IsNull();
    }
}
