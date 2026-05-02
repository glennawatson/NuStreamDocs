// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Logging;

namespace NuStreamDocs.Caching;

/// <summary>
/// Thread-safe size + age bounded LRU cache.
/// </summary>
/// <typeparam name="TKey">Key type; equality compared.</typeparam>
/// <typeparam name="TValue">Value type held by the cache.</typeparam>
/// <remarks>
/// Long-running watcher / build sessions can't accumulate cache
/// state without bound. <see cref="BoundedCache{TKey, TValue}"/> caps
/// both the entry count and the per-entry age:
/// <list type="bullet">
/// <item>Capacity-bound: the least-recently-used entry is evicted
/// when adding past <see cref="Capacity"/>.</item>
/// <item>Age-bound: <see cref="TryGet"/> evicts on read when an entry
/// is older than <see cref="MaxAge"/>; a periodic
/// <c>Compact</c> sweep drops anything
/// the read path hasn't touched.</item>
/// </list>
/// Built on a <see cref="LinkedList{T}"/> + <see cref="Dictionary{TKey, TValue}"/>:
/// O(1) get, set, and eviction; lock-protected so worker threads on
/// the parallel render pipeline can share one cache without tearing.
/// </remarks>
public sealed class BoundedCache<TKey, TValue>
    where TKey : notnull
{
    /// <summary>Synchronization root.</summary>
    private readonly Lock _gate = new();

    /// <summary>LRU recency list; Most-recently-used at the head.</summary>
    private readonly LinkedList<Entry> _order = [];

    /// <summary>Lookup from key to the order-list node holding the entry.</summary>
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _index;

    /// <summary>Wall-clock provider; injected so tests can substitute deterministic time.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Optional logger for eviction diagnostics; null suppresses logging.</summary>
    private readonly ILogger? _logger;

    /// <summary>Initializes a new instance of the <see cref="BoundedCache{TKey, TValue}"/> class.</summary>
    /// <param name="capacity">Maximum entry count. Must be positive.</param>
    /// <param name="maxAge">Maximum age before an entry is considered stale.</param>
    public BoundedCache(int capacity, in TimeSpan maxAge)
        : this(capacity, maxAge, equalityComparer: null, timeProvider: null, logger: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BoundedCache{TKey, TValue}"/> class.</summary>
    /// <param name="capacity">Maximum entry count. Must be positive.</param>
    /// <param name="maxAge">Maximum age before an entry is considered stale.</param>
    /// <param name="equalityComparer">Optional comparer for <typeparamref name="TKey"/>.</param>
    /// <param name="timeProvider">Optional wall-clock provider; defaults to <see cref="TimeProvider.System"/>.</param>
    public BoundedCache(int capacity, in TimeSpan maxAge, IEqualityComparer<TKey>? equalityComparer, TimeProvider? timeProvider)
        : this(capacity, maxAge, equalityComparer, timeProvider, logger: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BoundedCache{TKey, TValue}"/> class.</summary>
    /// <param name="capacity">Maximum entry count. Must be positive.</param>
    /// <param name="maxAge">Maximum age before an entry is considered stale.</param>
    /// <param name="equalityComparer">Optional comparer for <typeparamref name="TKey"/>.</param>
    /// <param name="timeProvider">Optional wall-clock provider; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="logger">Optional logger; eviction events are emitted at debug level when supplied.</param>
    public BoundedCache(int capacity, in TimeSpan maxAge, IEqualityComparer<TKey>? equalityComparer, TimeProvider? timeProvider, ILogger? logger)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        if (maxAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge), maxAge, "Max age must be positive.");
        }

        Capacity = capacity;
        MaxAge = maxAge;
        _index = new(capacity, equalityComparer ?? EqualityComparer<TKey>.Default);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger;
    }

    /// <summary>Gets the configured capacity bound.</summary>
    public int Capacity { get; }

    /// <summary>Gets the configured per-entry age bound.</summary>
    public TimeSpan MaxAge { get; }

    /// <summary>Gets the current entry count.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _index.Count;
            }
        }
    }

    /// <summary>Inserts or replaces <paramref name="value"/> for <paramref name="key"/>.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to store.</param>
    public void Set(TKey key, TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                _order.Remove(existing);
                existing.Value.Value = value;
                existing.Value.WriteTime = now;
                _order.AddFirst(existing);
                return;
            }

            var node = new LinkedListNode<Entry>(new(key, value, now));
            _order.AddFirst(node);
            _index[key] = node;

            var evicted = 0;
            while (_index.Count > Capacity)
            {
                EvictTail();
                evicted++;
            }

            if (evicted > 0 && _logger != null)
            {
                LogInvokerHelper.Invoke(
                    _logger,
                    LogLevel.Debug,
                    evicted,
                    _index.Count,
                    static (l, removed, remaining) => CachingLoggingHelper.LogCacheEviction(l, "capacity", removed, remaining));
            }
        }
    }

    /// <summary>Tries to read the value for <paramref name="key"/>; evicts on age miss.</summary>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Resolved value on success.</param>
    /// <returns>True when a fresh entry was found.</returns>
    public bool TryGet(TKey key, out TValue value)
    {
        ArgumentNullException.ThrowIfNull(key);

        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (!_index.TryGetValue(key, out var node))
            {
                value = default!;
                return false;
            }

            if (now - node.Value.WriteTime > MaxAge)
            {
                _order.Remove(node);
                _index.Remove(key);
                value = default!;
                return false;
            }

            _order.Remove(node);
            _order.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
    }

    /// <summary>Removes <paramref name="key"/> if present.</summary>
    /// <param name="key">Cache key.</param>
    /// <returns>True when an entry was removed.</returns>
    public bool Remove(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (_gate)
        {
            if (!_index.TryGetValue(key, out var node))
            {
                return false;
            }

            _order.Remove(node);
            _index.Remove(key);
            return true;
        }
    }

    /// <summary>Evicts every entry older than <see cref="MaxAge"/> as of <paramref name="now"/>.</summary>
    /// <param name="now">Wall-clock instant to compare against.</param>
    /// <returns>Number of entries removed.</returns>
    public int Compact(in DateTimeOffset now)
    {
        var removed = 0;
        int remaining;
        lock (_gate)
        {
            var node = _order.Last;
            while (node is not null && now - node.Value.WriteTime > MaxAge)
            {
                var prev = node.Previous;
                _order.Remove(node);
                _index.Remove(node.Value.Key);
                removed++;
                node = prev;
            }

            remaining = _index.Count;
        }

        if (removed > 0 && _logger != null)
        {
            LogInvokerHelper.Invoke(
                _logger,
                LogLevel.Debug,
                removed,
                remaining,
                static (l, r, rem) => CachingLoggingHelper.LogCacheEviction(l, "compact", r, rem));
        }

        return removed;
    }

    /// <summary>Drops every entry.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _order.Clear();
            _index.Clear();
        }
    }

    /// <summary>Removes the LRU tail entry; caller holds <see cref="_gate"/>.</summary>
    private void EvictTail()
    {
        var tail = _order.Last;
        if (tail is null)
        {
            return;
        }

        _order.RemoveLast();
        _index.Remove(tail.Value.Key);
    }

    /// <summary>One bucket on the recency list.</summary>
    /// <remarks>
    /// Reference type so the same instance is shared by the
    /// <see cref="LinkedList{T}"/> node and the dictionary lookup —
    /// touching one mutates the other without copy back.
    /// </remarks>
    private sealed class Entry(TKey key, TValue value, DateTimeOffset writeTime)
    {
        /// <summary>Gets the key that addresses this entry.</summary>
        public TKey Key { get; } = key;

        /// <summary>Gets or sets the cached value.</summary>
        public TValue Value { get; set; } = value;

        /// <summary>Gets or sets the wall-clock time the entry was written.</summary>
        public DateTimeOffset WriteTime { get; set; } = writeTime;
    }
}
