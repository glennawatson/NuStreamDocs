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
/// Storage is byte-keyed (UTF-8 id bytes) so the per-marker scan resolves directly against the
/// source <see cref="ReadOnlySpan{Byte}"/> via <see cref="Dictionary{TKey, TValue}.AlternateLookup{TAlternateKey}"/>.
/// The <see cref="TryGet(string, out CitationEntry)"/> string overload encodes once into a stack
/// buffer and delegates to the byte path — used only by tests and diagnostics.
/// </remarks>
public sealed class BibliographyDatabase
{
    /// <summary>Stack buffer cap for the string-overload encode probe.</summary>
    private const int IdStackBuffer = 256;

    /// <summary>Byte-keyed citation lookup; the per-marker scanner probes this directly.</summary>
    private readonly Dictionary<byte[], CitationEntry> _byIdBytes;

    /// <summary>Stable insertion-order array used by the bibliography emitter.</summary>
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
    /// <remarks>
    /// String adapter for the byte-keyed primary path — encodes <paramref name="id"/> into a
    /// stack-or-pool buffer once and probes via
    /// <see cref="TryGet(ReadOnlySpan{byte}, out CitationEntry)"/>.
    /// </remarks>
    public bool TryGet(string id, [MaybeNullWhen(false)] out CitationEntry entry)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        Span<byte> stack = stackalloc byte[IdStackBuffer];
        var maxBytes = Encoding.UTF8.GetMaxByteCount(id.Length);
        var buffer = maxBytes <= stack.Length ? stack : new byte[maxBytes];
        var written = Encoding.UTF8.GetBytes(id, buffer);
        return TryGet(buffer[..written], out entry);
    }

    /// <summary>Tries to resolve a UTF-8-encoded citation id to its entry.</summary>
    /// <param name="id">Citation id bytes (no <c>@</c> prefix).</param>
    /// <param name="entry">Resolved entry on hit.</param>
    /// <returns>True when the id is in the database.</returns>
    /// <remarks>The hot-path lookup; the per-marker scanner probes this directly with the source span.</remarks>
    public bool TryGet(ReadOnlySpan<byte> id, [MaybeNullWhen(false)] out CitationEntry entry) =>
        _byIdBytes.TryGetValueByUtf8(id, out entry!);
}
