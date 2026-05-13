// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="UnicodeRangeMatcher"/>.</summary>
public class UnicodeRangeMatcherTests
{
    /// <summary>A fresh bitset has only block 0 (ASCII) set; <c>MarkSeen</c> sets the blocks the text touches.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkSeenSetsTouchedBlocks()
    {
        var ascii = UnicodeRangeMatcher.NewSeenBlocks();
        UnicodeRangeMatcher.MarkSeen(Encoding.UTF8.GetBytes("Hello, world!"), ascii);
        await Assert.That(ascii[0]).IsTrue();
        await Assert.That(ascii[4]).IsFalse(); // U+0400 block — no Cyrillic seen.

        var withCyrillic = UnicodeRangeMatcher.NewSeenBlocks();
        UnicodeRangeMatcher.MarkSeen(Encoding.UTF8.GetBytes("Привет"), withCyrillic); // П = U+041F → block 4.
        await Assert.That(withCyrillic[0]).IsTrue();
        await Assert.That(withCyrillic[4]).IsTrue();
    }

    /// <summary>An ASCII-only bitset overlaps the Latin range but not the Cyrillic range.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OverlapsRespectsSeenBlocks()
    {
        var ascii = UnicodeRangeMatcher.NewSeenBlocks();
        await Assert.That(UnicodeRangeMatcher.Overlaps("U+0000-00FF, U+0131, U+0152-0153"u8, ascii)).IsTrue();
        await Assert.That(UnicodeRangeMatcher.Overlaps("U+00??"u8, ascii)).IsTrue();
        await Assert.That(UnicodeRangeMatcher.Overlaps("U+0400-045F"u8, ascii)).IsFalse();
        await Assert.That(UnicodeRangeMatcher.Overlaps("U+0100-024F"u8, ascii)).IsFalse();

        var withCyrillic = UnicodeRangeMatcher.NewSeenBlocks();
        UnicodeRangeMatcher.MarkSeen(Encoding.UTF8.GetBytes("Привет"), withCyrillic);
        await Assert.That(UnicodeRangeMatcher.Overlaps("U+0400-045F"u8, withCyrillic)).IsTrue();

        var withLatinExt = UnicodeRangeMatcher.NewSeenBlocks();
        UnicodeRangeMatcher.MarkSeen(
            Encoding.UTF8.GetBytes("café"),
            withLatinExt); // é = U+00E9 → block 0, but ē U+0113 would be block 1; here only block 0.
        await Assert.That(UnicodeRangeMatcher.Overlaps("U+0100-024F"u8, withLatinExt)).IsFalse();
        UnicodeRangeMatcher.MarkSeen(Encoding.UTF8.GetBytes("Tōkyō"), withLatinExt); // ō = U+014D → block 1.
        await Assert.That(UnicodeRangeMatcher.Overlaps("U+0100-024F"u8, withLatinExt)).IsTrue();
    }

    /// <summary>A garbled range value never throws and simply doesn't overlap.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GarbledRangeDoesNotOverlap()
    {
        var ascii = UnicodeRangeMatcher.NewSeenBlocks();
        await Assert.That(UnicodeRangeMatcher.Overlaps("not a range"u8, ascii)).IsFalse();
        await Assert.That(UnicodeRangeMatcher.Overlaps([], ascii)).IsFalse();
        await Assert.That(UnicodeRangeMatcher.Overlaps("U+04ZZ"u8, ascii)).IsFalse();
    }
}
