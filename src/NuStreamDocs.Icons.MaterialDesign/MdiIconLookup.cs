// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>
/// Byte-keyed icon lookup backed by a <see cref="FrozenDictionary{TKey, TValue}"/>
/// of <c>byte[]</c> name → <c>(offset, length)</c> slice over a single
/// concatenated SVG-path blob, plus an <see cref="IAlternateEqualityComparer{TAlternate, T}"/>
/// that lets the rewriter look up by <see cref="ReadOnlySpan{T}"/>
/// without ever allocating a <c>byte[]</c> per call.
/// </summary>
/// <remarks>
/// Layout was chosen for the MDI use case (~7000 icons, ~1.5 MB total
/// path bytes). Storing each path as its own <c>byte[]</c> would mean
/// 7000+ heap objects + dictionary boxing on cold start; one big blob
/// + flat offset table keeps the working set in a couple of arrays
/// the GC ignores after init.
/// </remarks>
public sealed class MdiIconLookup
{
    /// <summary>The concatenated SVG-path blob — every icon's bytes laid end-to-end.</summary>
    private readonly byte[] _blob;

    /// <summary>Frozen <c>name → (blobOffset, blobLength)</c> map.</summary>
    private readonly FrozenDictionary<byte[], (int Offset, int Length)> _index;

    /// <summary>Alt-lookup that takes <see cref="ReadOnlySpan{T}"/> name spans without materialising a <c>byte[]</c>.</summary>
    private readonly FrozenDictionary<byte[], (int Offset, int Length)>.AlternateLookup<ReadOnlySpan<byte>> _altLookup;

    /// <summary>Initializes a new instance of the <see cref="MdiIconLookup"/> class.</summary>
    /// <param name="blob">Concatenated SVG bytes.</param>
    /// <param name="index">Per-icon <c>name → (offset, length)</c> mapping into <paramref name="blob"/>.</param>
    public MdiIconLookup(byte[] blob, FrozenDictionary<byte[], (int Offset, int Length)> index)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentNullException.ThrowIfNull(index);
        _blob = blob;
        _index = index;
        _altLookup = _index.GetAlternateLookup<ReadOnlySpan<byte>>();
    }

    /// <summary>Gets the number of icons in the lookup.</summary>
    public int Count => _index.Count;

    /// <summary>Tries to resolve <paramref name="iconName"/> to its SVG bytes.</summary>
    /// <param name="iconName">UTF-8 icon name (no <c>material-</c> prefix, no surrounding colons).</param>
    /// <param name="svg">UTF-8 SVG bytes on hit.</param>
    /// <returns>True when the icon is in the lookup.</returns>
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Instance member by design — encapsulates the per-build blob/index pair.")]
    public bool TryGet(ReadOnlySpan<byte> iconName, out ReadOnlySpan<byte> svg)
    {
        if (_altLookup.TryGetValue(iconName, out var slice))
        {
            svg = _blob.AsSpan(slice.Offset, slice.Length);
            return true;
        }

        svg = default;
        return false;
    }
}
