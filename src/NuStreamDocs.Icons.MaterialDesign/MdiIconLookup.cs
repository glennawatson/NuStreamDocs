// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Common;

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>Resolves a Material Design icon name to its SVG path-data bytes.</summary>
public sealed class MdiIconLookup
{
    /// <summary>The concatenated SVG-path blob — every icon's bytes laid end-to-end.</summary>
    private readonly byte[] _blob;

    /// <summary>Frozen <c>name → (blobOffset, blobLength)</c> map.</summary>
    private readonly Dictionary<byte[], (int Offset, int Length)> _index;

    /// <summary>Alt-lookup that takes <see cref="ReadOnlySpan{T}"/> name spans without materializing a <c>byte[]</c>.</summary>
    private readonly Dictionary<byte[], (int Offset, int Length)>.AlternateLookup<ReadOnlySpan<byte>> _altLookup;

    /// <summary>Initializes a new instance of the <see cref="MdiIconLookup"/> class.</summary>
    /// <param name="blob">Concatenated SVG bytes.</param>
    /// <param name="index">Per-icon <c>name → (offset, length)</c> mapping into <paramref name="blob"/>.</param>
    public MdiIconLookup(byte[] blob, Dictionary<byte[], (int Offset, int Length)> index)
    {
        ArgumentNullException.ThrowIfNull(blob);
        ArgumentNullException.ThrowIfNull(index);
        _blob = blob;
        _index = index;
        _altLookup = _index.AsUtf8Lookup();
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
