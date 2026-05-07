// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Autorefs.Tests;

/// <summary>Direct unit tests for <c>AutorefScanner</c> — the byte-level helpers extracted out of the rewriter.</summary>
public class AutorefScannerTests
{
    /// <summary>Marker prefix is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkerPrefixIsStable() =>
        await Assert.That(Encoding.UTF8.GetString(AutorefScanner.Marker)).IsEqualTo("@autoref:");

    /// <summary>TryFindNext locates a marker and returns its match offsets.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryFindNextLocatesMarker()
    {
        var src = "before @autoref:System.String more"u8;
        var ok = AutorefScanner.TryFindNext(src, 0, out var match);
        await Assert.That(ok).IsTrue();
        await Assert.That(match.MarkerStart).IsEqualTo("before ".Length);
        await Assert.That(match.IdStart).IsEqualTo("before @autoref:".Length);
        await Assert.That(match.IdEnd).IsEqualTo("before @autoref:System.String".Length);
    }

    /// <summary>TryFindNext returns false when no marker is present.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryFindNextReturnsFalseWhenAbsent()
    {
        var ok = AutorefScanner.TryFindNext("just plain html"u8, 0, out var match);
        await Assert.That(ok).IsFalse();
        await Assert.That(match == default).IsTrue();
    }

    /// <summary>TryFindNext at-or-past EOF returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryFindNextPastEndIsFalse()
    {
        byte[] bytes = [.. "abc"u8];
        var len = bytes.Length;
        var pastEnd = AutorefScanner.TryFindNext(bytes, len, out _);
        var farPast = AutorefScanner.TryFindNext(bytes, len + 5, out _);
        await Assert.That(pastEnd).IsFalse();
        await Assert.That(farPast).IsFalse();
    }

    /// <summary>A marker that runs to EOF reports IdEnd at source length.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryFindNextRunsToEnd()
    {
        byte[] bytes = [.. "@autoref:System.IDisposable"u8];
        var ok = AutorefScanner.TryFindNext(bytes, 0, out var match);
        await Assert.That(ok).IsTrue();
        await Assert.That(match.IdEnd).IsEqualTo(bytes.Length);
    }

    /// <summary>FindIdEnd stops at any of the registered terminator bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FindIdEndStopsAtTerminator()
    {
        await Assert.That(AutorefScanner.FindIdEnd("Id\""u8, 0)).IsEqualTo(2);
        await Assert.That(AutorefScanner.FindIdEnd("Id'"u8, 0)).IsEqualTo(2);
        await Assert.That(AutorefScanner.FindIdEnd("Id "u8, 0)).IsEqualTo(2);
        await Assert.That(AutorefScanner.FindIdEnd("Id<"u8, 0)).IsEqualTo(2);
        await Assert.That(AutorefScanner.FindIdEnd("Id>"u8, 0)).IsEqualTo(2);
        await Assert.That(AutorefScanner.FindIdEnd("Id\n"u8, 0)).IsEqualTo(2);
        await Assert.That(AutorefScanner.FindIdEnd("Id\r"u8, 0)).IsEqualTo(2);
        await Assert.That(AutorefScanner.FindIdEnd("Id\t"u8, 0)).IsEqualTo(2);
    }

    /// <summary>FindIdEnd treats a trailing method signature as part of the ID rather than truncating at <c>(</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FindIdEndIncludesMethodSignature()
    {
        var src = "M:Foo.Bar(System.Int32,System.String)\""u8;
        var end = AutorefScanner.FindIdEnd(src, 0);
        await Assert.That(end).IsEqualTo("M:Foo.Bar(System.Int32,System.String)".Length);
    }

    /// <summary>FindIdEnd returns source length when start is past the end.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FindIdEndPastEndReturnsLength()
    {
        byte[] bytes = [.. "abc"u8];
        var atEnd = AutorefScanner.FindIdEnd(bytes, bytes.Length);
        var pastEnd = AutorefScanner.FindIdEnd(bytes, bytes.Length + 1);
        await Assert.That(atEnd).IsEqualTo(bytes.Length);
        await Assert.That(pastEnd).IsEqualTo(bytes.Length);
    }
}
