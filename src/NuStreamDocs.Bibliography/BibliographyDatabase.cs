// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Bibliography.Model;
using NuStreamDocs.Common;

namespace NuStreamDocs.Bibliography;

/// <summary>
/// Lookup of <see cref="CitationEntry"/> records keyed by
/// <see cref="CitationEntry.Id"/>.
/// </summary>
public sealed class BibliographyDatabase
{
    /// <summary>Byte-keyed citation lookup.</summary>
    private readonly Dictionary<byte[], CitationEntry> _byIdBytes;

    /// <summary>Entries in insertion order.</summary>
    private readonly CitationEntry[] _ordered;

    /// <summary>Initializes a new instance of the <see cref="BibliographyDatabase"/> class.</summary>
    /// <param name="entries">All entries; duplicates by <c>id</c> are rejected.</param>
    public BibliographyDatabase(IReadOnlyList<CitationEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _ordered = [.. entries];
        Dictionary<byte[], CitationEntry> byteDict = new(entries.Count, ByteArrayComparer.Instance);
        for (var i = 0; i < _ordered.Length; i++)
        {
            var entry = _ordered[i];
            if (entry.Id is null or [])
            {
                throw new ArgumentException("Citation entry id must be non-empty", nameof(entries));
            }

            if (!byteDict.TryAdd(entry.Id, entry))
            {
                throw new ArgumentException($"Duplicate citation id: {Encoding.UTF8.GetString(entry.Id)}", nameof(entries));
            }
        }

        _byIdBytes = byteDict;
    }

    /// <summary>Gets the empty database.</summary>
    public static BibliographyDatabase Empty { get; } = new([]);

    /// <summary>Gets the count of entries in the database.</summary>
    public int Count => _ordered.Length;

    /// <summary>Gets every entry in insertion order.</summary>
    public ReadOnlySpan<CitationEntry> All => _ordered;

    /// <summary>Tries to resolve a UTF-8-encoded citation id to its entry.</summary>
    /// <param name="id">Citation id bytes (no <c>@</c> prefix).</param>
    /// <param name="entry">Resolved entry on hit.</param>
    /// <returns>True when the id is in the database.</returns>
    public bool TryGet(ReadOnlySpan<byte> id, [MaybeNullWhen(false)] out CitationEntry entry) =>
        _byIdBytes.TryGetValueByUtf8(id, out entry!);
}
