// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Templating;

/// <summary>
/// Read-mostly data container handed to a <see cref="Template"/>'s <c>Render</c> method.
/// </summary>
/// <remarks>
/// Scalars are stored as <see cref="ReadOnlyMemory{T}"/> over UTF-8 bytes so callers
/// can hand in pooled / shared buffers without copying. Sections are arrays of nested
/// <see cref="TemplateData"/> scopes so iteration is a <c>for</c> loop over an indexed
/// array — no enumerator allocation.
/// <para>
/// Backed by byte-keyed <see cref="Dictionary{TKey, TValue}"/>s keyed ordinally on the
/// UTF-8 key bytes. The renderer probes via the
/// <see cref="Dictionary{TKey, TValue}.AlternateLookup{TAlternateKey}"/> shape so the
/// per-token resolve never allocates a string for the lookup probe.
/// </para>
/// <para>
/// Scalar lifetimes follow the caller: any <see cref="ReadOnlyMemory{T}"/> the caller
/// passes in must remain valid until <c>Render</c> returns. Theme plugins typically rent
/// the page body from <see cref="System.Buffers.ArrayPool{T}"/> and return after the
/// render call.
/// </para>
/// </remarks>
public sealed class TemplateData
{
    /// <summary>Empty-section sentinel reused everywhere.</summary>
    private static readonly TemplateData[] EmptySections = [];

    /// <summary>Empty scalar lookup reused for the empty-data scope.</summary>
    private static readonly Dictionary<byte[], ReadOnlyMemory<byte>> EmptyScalars = new(0, ByteArrayComparer.Instance);

    /// <summary>Empty section lookup reused for the empty-data scope.</summary>
    private static readonly Dictionary<byte[], TemplateData[]> EmptySectionMap = new(0, ByteArrayComparer.Instance);

    /// <summary>Span-keyed alternate lookup over the scalar map.</summary>
    /// <remarks>The struct holds a reference to the underlying <see cref="Dictionary{TKey, TValue}"/> so it stays alive for the lifetime of this instance.</remarks>
    private readonly Dictionary<byte[], ReadOnlyMemory<byte>>.AlternateLookup<ReadOnlySpan<byte>> _scalarLookup;

    /// <summary>Span-keyed alternate lookup over the section map.</summary>
    private readonly Dictionary<byte[], TemplateData[]>.AlternateLookup<ReadOnlySpan<byte>> _sectionLookup;

    /// <summary>Initializes a new instance of the <see cref="TemplateData"/> class with byte-keyed maps.</summary>
    /// <param name="scalars">UTF-8-byte-keyed scalar lookup. May be null for the empty case.</param>
    /// <param name="sections">UTF-8-byte-keyed section lookup. May be null for the empty case.</param>
    public TemplateData(Dictionary<byte[], ReadOnlyMemory<byte>>? scalars, Dictionary<byte[], TemplateData[]>? sections)
    {
        var scalarMap = scalars is { Count: > 0 }
            ? new(scalars, ByteArrayComparer.Instance)
            : EmptyScalars;
        var sectionMap = sections is { Count: > 0 }
            ? new(sections, ByteArrayComparer.Instance)
            : EmptySectionMap;
        _scalarLookup = scalarMap.AsUtf8Lookup();
        _sectionLookup = sectionMap.AsUtf8Lookup();
    }

    /// <summary>Gets the empty data scope.</summary>
    public static TemplateData Empty { get; } = new((Dictionary<byte[], ReadOnlyMemory<byte>>?)null, null);

    /// <summary>Tries to read a scalar value by UTF-8 key.</summary>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <param name="value">UTF-8 value bytes on success.</param>
    /// <returns>True when a scalar exists under <paramref name="key"/>.</returns>
    public bool TryGetScalar(ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
    {
        if (_scalarLookup.TryGetValue(key, out var bytes))
        {
            value = bytes.Span;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Returns the section items for <paramref name="key"/>, or an empty array when the key is missing or empty.</summary>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <returns>Section items.</returns>
    public TemplateData[] GetSection(ReadOnlySpan<byte> key) =>
        _sectionLookup.TryGetValue(key, out var items) ? items : EmptySections;

    /// <summary>True when <paramref name="key"/> resolves to a non-empty section or a non-empty scalar.</summary>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <returns>Truthiness for Mustache section semantics.</returns>
    public bool IsTruthy(ReadOnlySpan<byte> key) =>
        (_sectionLookup.TryGetValue(key, out var items) && items.Length > 0)
        || (_scalarLookup.TryGetValue(key, out var bytes) && bytes.Length > 0);
}
