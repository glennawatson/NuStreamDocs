// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;

namespace NuStreamDocs.Building;

/// <summary>
/// Thread-static cache of <see cref="ArrayBufferWriter{T}"/> instances
/// used as the per-page UTF-8 HTML output buffer (and now also as
/// preprocessor ping-pong scratch buffers).
/// </summary>
/// <remarks>
/// Two-tier pool: a thread-static slot array keeps the hot rent/return
/// pair allocation- and contention-free when a single thread does the
/// whole page (no async hop). A shared <see cref="ConcurrentQueue{T}"/>
/// catches writers when an async continuation Disposes the rental on a
/// different worker than the one that Rented — without it the
/// thread-static cache would miss every cross-thread page and we'd
/// allocate fresh writers per page on a 13K-page corpus.
/// <para>
/// Both tiers cap at <see cref="MaxCachedCapacity"/> per writer so an
/// outlier page doesn't pin multi-MB buffers, and the shared queue is
/// bounded at <see cref="MaxSharedPoolSize"/> entries so the pool
/// stops growing once warm.
/// </para>
/// </remarks>
public static class PageBuilderPool
{
    /// <summary>Default initial capacity hint when the caller passes 0.</summary>
    private const int DefaultHintCapacity = 4 * 1024;

    /// <summary>Cap above which a returned writer is dropped instead of parked back.</summary>
    private const int MaxCachedCapacity = 256 * 1024;

    /// <summary>Number of parked writer slots per thread; covers the page-output buffer plus a couple of preprocessor ping-pong scratch buffers.</summary>
    private const int MaxParkedSlots = 4;

    /// <summary>Cap on the shared cross-thread fallback pool. Roughly 4× the configured worker count plus a slack margin.</summary>
    private const int MaxSharedPoolSize = 64;

    /// <summary>Cross-thread fallback pool that catches Returns landing on a different thread than the originating Rent (typical when an <c>await</c> hops the continuation).</summary>
    private static readonly ConcurrentQueue<ArrayBufferWriter<byte>> SharedPool = new();

    /// <summary>Live count of writers currently parked in <see cref="SharedPool"/>; gates pushes once the cap is hit.</summary>
    private static int _sharedPoolCount;

    /// <summary>Per-thread parked writers ready for the next rental. Lazily allocated on first use.</summary>
    [ThreadStatic]
    private static ArrayBufferWriter<byte>?[]? _slots;

    /// <summary>Rents a UTF-8 buffer writer using the default capacity hint.</summary>
    /// <returns>A <see cref="PageBuilderRental"/> the caller disposes.</returns>
    public static PageBuilderRental Rent() => Rent(DefaultHintCapacity);

    /// <summary>Rents a UTF-8 buffer writer pre-sized to <paramref name="hintCapacity"/>.</summary>
    /// <param name="hintCapacity">Initial capacity hint, in bytes.</param>
    /// <returns>A <see cref="PageBuilderRental"/> the caller disposes.</returns>
    public static PageBuilderRental Rent(int hintCapacity)
    {
        // Hot path: this thread parked a writer recently (no async hop on the prior page).
        var slots = _slots;
        if (slots is not null)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var parked = slots[i];
                if (parked is null)
                {
                    continue;
                }

                slots[i] = null;
                parked.ResetWrittenCount();
                return new(parked);
            }
        }

        // Cross-thread path: drain the shared queue when an async continuation landed
        // a writer on a different thread than the one now Renting.
        if (SharedPool.TryDequeue(out var shared))
        {
            Interlocked.Decrement(ref _sharedPoolCount);
            shared.ResetWrittenCount();
            return new(shared);
        }

        return new(new(hintCapacity));
    }

    /// <summary>Returns <paramref name="writer"/> to the per-thread cache (or the shared queue if the slots are full).</summary>
    /// <param name="writer">The writer to park, already reset by the rental.</param>
    internal static void Return(ArrayBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (writer.Capacity > MaxCachedCapacity)
        {
            return;
        }

        _slots ??= new ArrayBufferWriter<byte>?[MaxParkedSlots];
        var slots = _slots;
        for (var i = 0; i < slots.Length; i++)
        {
            if (slots[i] is not null)
            {
                continue;
            }

            slots[i] = writer;
            return;
        }

        // Thread-static slots full — push to the shared queue so a later cross-thread
        // Rent on a different worker can pick it up. Capped to keep the pool bounded.
        if (Interlocked.Increment(ref _sharedPoolCount) <= MaxSharedPoolSize)
        {
            SharedPool.Enqueue(writer);
            return;
        }

        Interlocked.Decrement(ref _sharedPoolCount);
    }
}
