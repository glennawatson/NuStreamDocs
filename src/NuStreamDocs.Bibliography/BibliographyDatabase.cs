// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Bibliography.Model;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Read-mostly lookup of <see cref="CitationEntry"/> records keyed by
/// <see cref="CitationEntry.Id"/>. Built once at configure time and
/// queried on every <c>[@key]</c> resolution.
/// </summary>
public sealed class BibliographyDatabase
{
    /// <summary>Citation lookup; built once at configure, queried per [@key].</summary>
    private readonly Dictionary<string, CitationEntry> _byId;

    /// <summary>Stable insertion-order array used by the bibliography emitter.</summary>
    private readonly CitationEntry[] _ordered;

    /// <summary>Initializes a new instance of the <see cref="BibliographyDatabase"/> class.</summary>
    /// <param name="entries">All entries; duplicates by <c>id</c> are rejected.</param>
    public BibliographyDatabase(IReadOnlyList<CitationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _ordered = [.. entries];
        var dict = new Dictionary<string, CitationEntry>(entries.Count, StringComparer.Ordinal);
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
        }

        _byId = dict;
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
    public bool TryGet(string id, out CitationEntry? entry) => _byId.TryGetValue(id, out entry);
}
