// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Read-mostly lookup of <see cref="CitationEntry"/> records keyed by
/// <see cref="CitationEntry.Id"/>. Built once at configure time and
/// queried on every <c>[@key]</c> resolution.
/// </summary>
/// <remarks>
/// The hot citation-marker scan resolves keys against a byte-keyed
/// dictionary so it never round-trips through <see cref="string"/>.
/// The string-keyed dictionary is kept for the public <see cref="TryGet(string, out CitationEntry)"/>
/// API and missing-callback wiring.
/// </remarks>
public sealed class BibliographyDatabase
{
    /// <summary>Citation lookup keyed by string id; built once at configure, queried per public API call.</summary>
    private readonly Dictionary<string, CitationEntry> _byId;

    /// <summary>Byte-keyed lookup populated alongside <see cref="_byId"/>.</summary>
    /// <remarks>The scanner queries this with the source <see cref="ReadOnlySpan{Byte}"/> directly via <see cref="Dictionary{TKey, TValue}.AlternateLookup{TAlternateKey}"/>.</remarks>
    private readonly Dictionary<byte[], CitationEntry> _byIdBytes;

    /// <summary>Stable insertion-order array used by the bibliography emitter.</summary>
    private readonly CitationEntry[] _ordered;

    /// <summary>Initializes a new instance of the <see cref="BibliographyDatabase"/> class.</summary>
    /// <param name="entries">All entries; duplicates by <c>id</c> are rejected.</param>
    public BibliographyDatabase(IReadOnlyList<CitationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _ordered = [.. entries];
        var dict = new Dictionary<string, CitationEntry>(entries.Count, StringComparer.Ordinal);
        var byteDict = new Dictionary<byte[], CitationEntry>(entries.Count, ByteArrayComparer.Instance);
        for (var i = 0; i < _ordered.Length; i++)
        {
            var entry = _ordered[i];
            if (string.IsNullOrEmpty(entry.Id))
            {
                throw new ArgumentException("Citation entry id must be non-empty", nameof(entries));
            }

            if (!dict.TryAdd(entry.Id, entry))
            {
                throw new ArgumentException($"Duplicate citation id: {entry.Id}", nameof(entries));
            }

            byteDict.Add(Encoding.UTF8.GetBytes(entry.Id), entry);
        }

        _byId = dict;
        _byIdBytes = byteDict;
    }

    /// <summary>Gets the empty database — useful as the default option set.</summary>
    public static BibliographyDatabase Empty { get; } = new([]);

    /// <summary>Gets the count of entries in the database.</summary>
    public int Count => _ordered.Length;

    /// <summary>Gets every entry in insertion order.</summary>
    public ReadOnlySpan<CitationEntry> All => _ordered;

    /// <summary>Tries to resolve a citation id to its entry.</summary>
    /// <param name="id">Citation id.</param>
    /// <param name="entry">Resolved entry on hit.</param>
    /// <returns>True when the id is in the database.</returns>
    public bool TryGet(string id, [MaybeNullWhen(false)] out CitationEntry entry) =>
        _byId.TryGetValue(id, out entry);

    /// <summary>Tries to resolve a UTF-8-encoded citation id to its entry.</summary>
    /// <param name="id">Citation id bytes (no <c>@</c> prefix).</param>
    /// <param name="entry">Resolved entry on hit.</param>
    /// <returns>True when the id is in the database.</returns>
    /// <remarks>
    /// Probes the byte-keyed dictionary via <see cref="Dictionary{TKey, TValue}.GetAlternateLookup{TAlternateKey}"/>
    /// + <see cref="ByteArrayComparer"/> so the lookup hashes <paramref name="id"/> directly without
    /// materializing a <see cref="byte"/> array or a <see cref="string"/>.
    /// </remarks>
    public bool TryGet(ReadOnlySpan<byte> id, [MaybeNullWhen(false)] out CitationEntry entry) =>
        _byIdBytes.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(id, out entry);
}
