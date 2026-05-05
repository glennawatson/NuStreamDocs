// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using NuStreamDocs.Common;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Thread-safe map of cross-document reference IDs to the page URL +
/// fragment that owns them.
/// </summary>
public sealed class AutorefsRegistry
{
    /// <summary>Default pre-sized capacity for the parameterless constructor.</summary>
    /// <remarks>
    /// Sized to absorb typical small-to-medium sites (a few hundred to
    /// a couple of thousand registered headings) without bucket
    /// rehashes in the parallel render pass. For very large corpora
    /// pass a capacity hint via the <see cref="AutorefsRegistry(int)"/>
    /// overload — the bench data shows pre-sizing saves ~28% time and
    /// ~50% allocation across the register-heavy scenarios.
    /// </remarks>
    private const int DefaultInitialCapacity = 2_048;

    /// <summary>Default concurrency level when the caller does not pass a capacity hint.</summary>
    /// <remarks>
    /// Matches <see cref="ConcurrentDictionary{TKey,TValue}"/>'s default
    /// (number of cores). Kept explicit so the capacity overload below
    /// reuses the same value rather than re-deriving it.
    /// </remarks>
    private static readonly int DefaultConcurrencyLevel = Environment.ProcessorCount;

    /// <summary>Anchor index keyed on UTF-8 ID bytes.</summary>
    private readonly ConcurrentDictionary<byte[], Anchor> _anchors;

    /// <summary>Initializes a new instance of the <see cref="AutorefsRegistry"/> class pre-sized for the typical site (a few thousand entries).</summary>
    public AutorefsRegistry()
        : this(DefaultInitialCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AutorefsRegistry"/> class pre-sized for <paramref name="initialCapacity"/> entries.</summary>
    /// <param name="initialCapacity">Expected entry count — pass a hint when registering tens of thousands of headings to avoid bucket-resize churn.</param>
    public AutorefsRegistry(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
        _anchors = new(DefaultConcurrencyLevel, initialCapacity, ByteArrayComparer.Instance);
    }

    /// <summary>Gets the current entry count.</summary>
    public int Count => _anchors.Count;

    /// <summary>Registers an ID against a page URL and fragment using the byte-shaped hot path.</summary>
    /// <param name="id">UTF-8 reference ID bytes.</param>
    /// <param name="pageRelativeUrlBytes">
    /// UTF-8 page-relative URL bytes; the array reference is stored as canonical storage and shared
    /// across every ID registered for the same page, so encoding happens once per page rather than once per ID.
    /// Callers must not mutate the array after handing it in.
    /// </param>
    /// <param name="fragment">UTF-8 fragment bytes (without the leading <c>#</c>); pass an empty span for whole-page references.</param>
    public void Register(ReadOnlySpan<byte> id, byte[] pageRelativeUrlBytes, ReadOnlySpan<byte> fragment)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("id must be non-empty.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(pageRelativeUrlBytes);
        byte[] idBytes = [.. id];

        _anchors[idBytes] = new(pageRelativeUrlBytes, ResolveFragment(idBytes, fragment));
    }

    /// <summary>Resolves an ID to its full URL and writes the UTF-8 URL bytes directly into <paramref name="writer"/>.</summary>
    /// <param name="id">UTF-8 reference ID bytes.</param>
    /// <param name="writer">Sink the resolved URL bytes are written into when the ID resolves.</param>
    /// <returns>True when the ID was registered and bytes were written.</returns>
    /// <remarks>The hot-path resolve — the autoref rewriter calls this per marker. The writer state is unchanged on a miss, so the caller can fall back to emitting the original marker.</remarks>
    public bool TryResolveInto(ReadOnlySpan<byte> id, IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (!_anchors.TryGetValueByUtf8(id, out var anchor))
        {
            return false;
        }

        anchor.WriteInto(writer);
        return true;
    }

    /// <summary>Resolves an ID to its full URL as a freshly allocated UTF-8 byte array.</summary>
    /// <param name="id">UTF-8 reference ID bytes.</param>
    /// <param name="url">Resolved UTF-8 URL bytes on success; empty array on miss.</param>
    /// <returns>True when the ID was registered.</returns>
    /// <remarks>Allocating overload — diagnostics / snapshot path. The hot per-marker rewriter uses <see cref="TryResolveInto(ReadOnlySpan{byte}, IBufferWriter{byte})"/>.</remarks>
    public bool TryResolve(ReadOnlySpan<byte> id, out byte[] url)
    {
        if (!_anchors.TryGetValueByUtf8(id, out var anchor))
        {
            url = [];
            return false;
        }

        url = anchor.ComposeString();
        return true;
    }

    /// <summary>Drops every registered entry. Used between builds in watcher mode.</summary>
    public void Clear() => _anchors.Clear();

    /// <summary>Snapshots the registry into a right-sized <c>(id, url)</c> array. Order is unspecified.</summary>
    /// <returns>A fresh snapshot array.</returns>
    /// <remarks>Build-end consumer; pays one UTF-8 decode per entry. Hot-path resolution goes through <see cref="TryResolveInto(ReadOnlySpan{byte}, IBufferWriter{byte})"/>.</remarks>
    public (byte[] Id, byte[] Url)[] Snapshot()
    {
        KeyValuePair<byte[], Anchor>[] kvs = [.. _anchors];
        var result = new (byte[] Id, byte[] Url)[kvs.Length];
        for (var i = 0; i < kvs.Length; i++)
        {
            result[i] = (kvs[i].Key, kvs[i].Value.ComposeString());
        }

        return result;
    }

    /// <summary>Resolves the fragment byte storage for a register call.</summary>
    /// <param name="idBytes">Materialized id bytes (the dictionary key).</param>
    /// <param name="fragment">Fragment span supplied by the caller.</param>
    /// <returns>The byte storage for the fragment, or null when the caller passed an empty span.</returns>
    /// <remarks>
    /// When the fragment matches the just-materialized id bytes (the heading-scanner hot path), the same
    /// array is reused as the fragment — saving the second <c>ToArray</c>. Other callers with distinct
    /// fragments pay the per-fragment copy.
    /// </remarks>
    private static byte[]? ResolveFragment(byte[] idBytes, ReadOnlySpan<byte> fragment)
    {
        if (fragment.IsEmpty)
        {
            return null;
        }

        if (fragment.SequenceEqual(idBytes))
        {
            return idBytes;
        }

        return fragment.ToArray();
    }

    /// <summary>Stored anchor — page URL bytes + optional fragment bytes, composed only at write/snapshot time.</summary>
    /// <param name="PageUrl">UTF-8 page-relative URL.</param>
    /// <param name="Fragment">Optional UTF-8 anchor fragment without the leading <c>#</c>; null for whole-page references.</param>
    private readonly record struct Anchor(byte[] PageUrl, byte[]? Fragment)
    {
        /// <summary>Writes the composed URL (<c>page</c> or <c>page#fragment</c>) into <paramref name="writer"/> as UTF-8.</summary>
        /// <param name="writer">UTF-8 sink.</param>
        public void WriteInto(IBufferWriter<byte> writer)
        {
            Utf8StringWriter.Write(writer, PageUrl);
            if (Fragment is null)
            {
                return;
            }

            Utf8StringWriter.WriteByte(writer, (byte)'#');
            Utf8StringWriter.Write(writer, Fragment);
        }

        /// <summary>Composes the stored URL into <c>page</c> or <c>page#fragment</c> form as a string.</summary>
        /// <returns>Composed URL.</returns>
        public byte[] ComposeString() => Fragment is null
            ? PageUrl
            : Utf8Concat.Concat(PageUrl, "#"u8, Fragment);
    }
}
