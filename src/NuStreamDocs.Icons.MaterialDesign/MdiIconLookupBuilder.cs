// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Icons.MaterialDesign;

/// <summary>Builder for assembling an <see cref="MdiIconLookup"/> from <c>(name, svg)</c> pairs.</summary>
public sealed class MdiIconLookupBuilder
{
    /// <summary>Accumulated SVG bytes.</summary>
    private readonly List<byte> _blob = [];

    /// <summary>Per-icon name to slice mapping.</summary>
    private readonly Dictionary<byte[], (int Offset, int Length)> _entries = new(ByteArrayKeyComparer.Instance);

    /// <summary>Adds an icon to the lookup.</summary>
    /// <param name="name">UTF-8 icon name (no <c>material-</c> prefix).</param>
    /// <param name="svg">UTF-8 SVG bytes inlined verbatim on resolve.</param>
    /// <returns>This builder for chaining.</returns>
    public MdiIconLookupBuilder Add(ReadOnlySpan<byte> name, ReadOnlySpan<byte> svg)
    {
        var nameKey = name.ToArray();
        var offset = _blob.Count;
        for (var i = 0; i < svg.Length; i++)
        {
            _blob.Add(svg[i]);
        }

        _entries[nameKey] = (offset, svg.Length);
        return this;
    }

    /// <summary>Adds an icon whose name is a <see cref="string"/>.</summary>
    /// <param name="name">Icon name; encoded as UTF-8.</param>
    /// <param name="svg">UTF-8 SVG bytes.</param>
    /// <returns>This builder for chaining.</returns>
    public MdiIconLookupBuilder Add(string name, ReadOnlySpan<byte> svg)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        return Add(Encoding.UTF8.GetBytes(name), svg);
    }

    /// <summary>Builds the immutable lookup.</summary>
    /// <returns>The composed <see cref="MdiIconLookup"/>.</returns>
    public MdiIconLookup Build() => new([.. _blob], _entries);
}
