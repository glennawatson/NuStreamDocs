// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Equality coverage for PageBuilderRental.</summary>
public class PageBuilderRentalEqualityTests
{
    /// <summary>Two rentals owning the same writer compare equal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SameWriterEquals()
    {
        using var a = PageBuilderPool.Rent(16);
        var b = a;
        await Assert.That(a == b).IsTrue();
        await Assert.That(a != b).IsFalse();
        await Assert.That(a.Equals(b)).IsTrue();
        await Assert.That(a.Equals((object)b)).IsTrue();
        await Assert.That(a.GetHashCode()).IsEqualTo(b.GetHashCode());
    }

    /// <summary>Two distinct rentals compare not equal.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DifferentWriterNotEqual()
    {
        using var a = PageBuilderPool.Rent(16);
        using var b = PageBuilderPool.Rent(16);
        await Assert.That(a != b).IsTrue();
        await Assert.That(a.Equals((object?)null)).IsFalse();
        await Assert.That(a.Equals("not a rental")).IsFalse();
    }

    /// <summary>Default rental hashes to zero.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultRentalHash()
    {
        var def = default(PageBuilderRental);
        await Assert.That(def.GetHashCode()).IsEqualTo(0);
        def.Dispose();
    }
}
