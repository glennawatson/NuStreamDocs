// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Time.Testing;
using NuStreamDocs.Caching;

namespace NuStreamDocs.Tests;

/// <summary>Behaviour tests for <c>BoundedCache{TKey, TValue}</c>.</summary>
public class BoundedCacheTests
{
    /// <summary>Adding past <c>Capacity</c> evicts the least-recently-used entry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task EvictsLeastRecentlyUsedAtCapacity()
    {
        var cache = new BoundedCache<string, int>(2, TimeSpan.FromMinutes(10));
        cache.Set("a", 1);
        cache.Set("b", 2);
        _ = cache.TryGet("a", out _); // touch a — b becomes LRU
        cache.Set("c", 3);

        await Assert.That(cache.TryGet("a", out var a)).IsTrue();
        await Assert.That(a).IsEqualTo(1);
        await Assert.That(cache.TryGet("b", out _)).IsFalse();
        await Assert.That(cache.TryGet("c", out var c)).IsTrue();
        await Assert.That(c).IsEqualTo(3);
    }

    /// <summary>A read past <c>MaxAge</c> evicts the entry and reports a miss.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryGetEvictsAgedEntry()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var cache = new BoundedCache<string, int>(8, TimeSpan.FromSeconds(5), equalityComparer: null, timeProvider: time);
        cache.Set("k", 42);

        time.Advance(TimeSpan.FromSeconds(6));
        await Assert.That(cache.TryGet("k", out _)).IsFalse();
        await Assert.That(cache.Count).IsEqualTo(0);
    }

    /// <summary>A fresh read returns the value and refreshes recency.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryGetReturnsFreshEntry()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var cache = new BoundedCache<string, int>(8, TimeSpan.FromSeconds(5), equalityComparer: null, timeProvider: time);
        cache.Set("k", 7);
        time.Advance(TimeSpan.FromSeconds(2));

        await Assert.That(cache.TryGet("k", out var v)).IsTrue();
        await Assert.That(v).IsEqualTo(7);
    }

    /// <summary><c>BoundedCache{TKey, TValue}.Compact</c> drops every aged entry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CompactRemovesAgedEntries()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var cache = new BoundedCache<string, int>(8, TimeSpan.FromSeconds(5), equalityComparer: null, timeProvider: time);
        cache.Set("a", 1);
        cache.Set("b", 2);
        time.Advance(TimeSpan.FromSeconds(3));
        cache.Set("c", 3); // newer

        time.Advance(TimeSpan.FromSeconds(3)); // a, b now > 5s old; c is 3s old
        var removed = cache.Compact(time.GetUtcNow());

        await Assert.That(removed).IsEqualTo(2);
        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(cache.TryGet("c", out _)).IsTrue();
    }

    /// <summary><c>BoundedCache{TKey, TValue}.Set</c> on an existing key refreshes recency and timestamp.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SetRefreshesExistingEntry()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var cache = new BoundedCache<string, int>(2, TimeSpan.FromSeconds(5), equalityComparer: null, timeProvider: time);
        cache.Set("a", 1);
        cache.Set("b", 2);
        time.Advance(TimeSpan.FromSeconds(4));
        cache.Set("a", 11); // refresh a; b should now be LRU
        cache.Set("c", 3);  // pushes b out

        await Assert.That(cache.TryGet("b", out _)).IsFalse();
        await Assert.That(cache.TryGet("a", out var a)).IsTrue();
        await Assert.That(a).IsEqualTo(11);
    }

    /// <summary><c>BoundedCache{TKey, TValue}.Remove</c> drops the entry and reports success exactly once.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RemoveDropsEntry()
    {
        var cache = new BoundedCache<string, int>(4, TimeSpan.FromMinutes(1));
        cache.Set("a", 1);
        await Assert.That(cache.Remove("a")).IsTrue();
        await Assert.That(cache.Remove("a")).IsFalse();
        await Assert.That(cache.TryGet("a", out _)).IsFalse();
    }

    /// <summary>Constructor rejects non-positive capacity and age.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ConstructorRejectsInvalidArguments()
    {
        await Assert.That(static () => new BoundedCache<string, int>(0, TimeSpan.FromSeconds(1)))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => new BoundedCache<string, int>(4, TimeSpan.Zero))
            .Throws<ArgumentOutOfRangeException>();
    }

    /// <summary>Set and TryGet reject null keys.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NullKeyRejected()
    {
        var cache = new BoundedCache<string, int>(2, TimeSpan.FromMinutes(1));
        await Assert.That(() => cache.Set(null!, 1)).Throws<ArgumentNullException>();
        await Assert.That(() => cache.TryGet(null!, out _)).Throws<ArgumentNullException>();
        await Assert.That(() => cache.Remove(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>TryGet on an empty cache returns false.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryGetMissOnEmpty()
    {
        var cache = new BoundedCache<string, int>(2, TimeSpan.FromMinutes(1));
        await Assert.That(cache.TryGet("missing", out _)).IsFalse();
    }

    /// <summary>Compact on an empty cache returns 0 and does not touch state.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CompactOnEmptyReturnsZero()
    {
        var cache = new BoundedCache<string, int>(2, TimeSpan.FromMinutes(1));
        await Assert.That(cache.Compact(DateTimeOffset.UtcNow)).IsEqualTo(0);
    }

    /// <summary>Compact when nothing has aged returns 0 entries removed.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CompactWithFreshEntriesNoOp()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var cache = new BoundedCache<string, int>(8, TimeSpan.FromMinutes(10), equalityComparer: null, timeProvider: time);
        cache.Set("k", 1);
        await Assert.That(cache.Compact(time.GetUtcNow())).IsEqualTo(0);
        await Assert.That(cache.Count).IsEqualTo(1);
    }

    /// <summary>Clear drops every entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClearEmptiesCache()
    {
        var cache = new BoundedCache<string, int>(8, TimeSpan.FromMinutes(1));
        cache.Set("a", 1);
        cache.Set("b", 2);
        cache.Clear();
        await Assert.That(cache.Count).IsEqualTo(0);
        await Assert.That(cache.TryGet("a", out _)).IsFalse();
    }

    /// <summary>An explicit equality comparer routes lookups through the supplied comparer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomComparerUsedForLookup()
    {
        var cache = new BoundedCache<string, int>(4, TimeSpan.FromMinutes(1), StringComparer.OrdinalIgnoreCase, timeProvider: null);
        cache.Set("Key", 7);
        await Assert.That(cache.TryGet("KEY", out var v)).IsTrue();
        await Assert.That(v).IsEqualTo(7);
    }

    /// <summary>Capacity and MaxAge round-trip through their public properties.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PropertiesReflectConstructorArguments()
    {
        var cache = new BoundedCache<string, int>(7, TimeSpan.FromSeconds(13));
        await Assert.That(cache.Capacity).IsEqualTo(7);
        await Assert.That(cache.MaxAge).IsEqualTo(TimeSpan.FromSeconds(13));
    }
}
