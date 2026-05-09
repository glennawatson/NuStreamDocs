// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using NuStreamDocs.Common;

namespace NuStreamDocs.Autorefs;

/// <summary>Thread-safe map of cross-document reference IDs to the page URL and anchor fragment that own them.</summary>
public sealed class AutorefsRegistry
{
    /// <summary>Default capacity for the parameterless constructor; pass an explicit capacity for sites with more than ~15 K registered IDs.</summary>
    private const int DefaultInitialCapacity = 16_384;

    /// <summary>Default concurrency level for the underlying dictionary.</summary>
    private static readonly int DefaultConcurrencyLevel = Environment.ProcessorCount;

    /// <summary>Anchor index keyed on UTF-8 ID bytes.</summary>
    private readonly ConcurrentDictionary<byte[], Anchor> _anchors;

    /// <summary>Initializes a new instance of the <see cref="AutorefsRegistry"/> class with the default capacity.</summary>
    public AutorefsRegistry()
        : this(DefaultInitialCapacity)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AutorefsRegistry"/> class sized for <paramref name="initialCapacity"/> entries.</summary>
    /// <param name="initialCapacity">Expected entry count.</param>
    public AutorefsRegistry(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialCapacity);
        _anchors = new(DefaultConcurrencyLevel, initialCapacity, ByteArrayComparer.Instance);
    }

    /// <summary>Gets the current entry count.</summary>
    public int Count => _anchors.Count;

    /// <summary>Registers an ID against a page URL and fragment.</summary>
    /// <param name="id">UTF-8 reference ID bytes.</param>
    /// <param name="pageRelativeUrlBytes">UTF-8 page-relative URL bytes; the array reference is stored directly and must not be mutated after the call.</param>
    /// <param name="fragment">UTF-8 fragment bytes without the leading <c>#</c>; pass an empty span for whole-page references.</param>
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

    /// <summary>Resolves an ID and writes the UTF-8 URL bytes into <paramref name="writer"/>.</summary>
    /// <param name="id">UTF-8 reference ID bytes.</param>
    /// <param name="writer">Sink for the resolved URL bytes; left untouched on a miss.</param>
    /// <returns>True when the ID was registered.</returns>
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

    /// <summary>Resolves an ID to its full URL as a UTF-8 byte array.</summary>
    /// <param name="id">UTF-8 reference ID bytes.</param>
    /// <param name="url">Resolved UTF-8 URL bytes on success; empty array on miss.</param>
    /// <returns>True when the ID was registered.</returns>
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

    /// <summary>Drops every registered entry.</summary>
    public void Clear() => _anchors.Clear();

    /// <summary>Snapshots the registry into a fresh <c>(id, url)</c> array; order is unspecified.</summary>
    /// <returns>A fresh snapshot array.</returns>
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
    /// <param name="idBytes">Materialized id bytes.</param>
    /// <param name="fragment">Fragment span supplied by the caller.</param>
    /// <returns>The fragment storage, or null when <paramref name="fragment"/> is empty.</returns>
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

    /// <summary>Stored anchor — page URL plus optional fragment.</summary>
    /// <param name="PageUrl">UTF-8 page-relative URL.</param>
    /// <param name="Fragment">UTF-8 anchor fragment without the leading <c>#</c>; null for whole-page references.</param>
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

        /// <summary>Composes the stored URL into <c>page</c> or <c>page#fragment</c> form.</summary>
        /// <returns>Composed URL.</returns>
        public byte[] ComposeString() => Fragment is null
            ? PageUrl
            : Utf8Concat.Concat(PageUrl, "#"u8, Fragment);
    }
}
