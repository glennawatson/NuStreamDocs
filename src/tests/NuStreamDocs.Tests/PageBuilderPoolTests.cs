// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Tests for the page-builder rental pool.</summary>
public class PageBuilderPoolTests
{
    /// <summary>Rent + dispose returns the writer to the per-thread slot, so a follow-up rent reuses it.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RentReturnRoundTripReusesWriter()
    {
        using (var rental = PageBuilderPool.Rent(1024))
        {
            var first = rental.Writer;
            first.Write("hello"u8);
        }

        using var rental2 = PageBuilderPool.Rent(1024);
        await Assert.That(rental2.Writer.WrittenCount).IsEqualTo(0);
    }

    /// <summary>Rent without a hint defers to the default capacity overload.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RentWithoutHint()
    {
        using var rental = PageBuilderPool.Rent();
        await Assert.That(rental.Writer).IsNotNull();
    }

    /// <summary>Renting more writers than the per-thread slot count still works (shared queue path).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ManyConcurrentRentalsPushToSharedQueue()
    {
        var rentals = new List<PageBuilderRental>();
        try
        {
            for (var i = 0; i < 10; i++)
            {
                rentals.Add(PageBuilderPool.Rent(512));
            }

            await Assert.That(rentals.Count).IsEqualTo(10);
        }
        finally
        {
            foreach (var rental in rentals)
            {
                rental.Dispose();
            }
        }
    }

    /// <summary>Rentals served from the cache start with a zero WrittenCount even after the previous user wrote bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RentalIsResetBetweenUses()
    {
        using (var first = PageBuilderPool.Rent(512))
        {
            first.Writer.Write("data"u8);
        }

        using var second = PageBuilderPool.Rent(512);
        await Assert.That(second.Writer.WrittenCount).IsEqualTo(0);
    }
}
