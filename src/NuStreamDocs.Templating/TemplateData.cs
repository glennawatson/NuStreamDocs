// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

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
/// Backed by plain <see cref="Dictionary{TKey, TValue}"/> keyed ordinally. The data
/// scope is per-render and small (typically ≤ 32 entries each for scalars and
/// sections), so the freeze cost of <c>Dictionary</c> wouldn't repay itself —
/// `Dictionary` lookup is already O(1) and the per-render shaping cost dominates
/// any constant-factor difference.
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
    private static readonly Dictionary<string, ReadOnlyMemory<byte>> EmptyScalars = new(0, StringComparer.Ordinal);

    /// <summary>Empty section lookup reused for the empty-data scope.</summary>
    private static readonly Dictionary<string, TemplateData[]> EmptySectionMap = new(0, StringComparer.Ordinal);

    /// <summary>Scalar lookup keyed ordinally over UTF-8-decoded keys.</summary>
    private readonly Dictionary<string, ReadOnlyMemory<byte>> _scalars;

    /// <summary>Section lookup; values are pre-sized arrays of nested scopes.</summary>
    private readonly Dictionary<string, TemplateData[]> _sections;

    /// <summary>Initializes a new instance of the <see cref="TemplateData"/> class.</summary>
    /// <param name="scalars">Scalar lookup. May be null for the empty case.</param>
    /// <param name="sections">Section lookup. May be null for the empty case.</param>
    public TemplateData(Dictionary<string, ReadOnlyMemory<byte>>? scalars, Dictionary<string, TemplateData[]>? sections)
    {
        _scalars = scalars is { Count: > 0 }
            ? new(scalars, StringComparer.Ordinal)
            : EmptyScalars;
        _sections = sections is { Count: > 0 }
            ? new(sections, StringComparer.Ordinal)
            : EmptySectionMap;
    }

    /// <summary>Gets the empty data scope.</summary>
    public static TemplateData Empty { get; } = new((Dictionary<string, ReadOnlyMemory<byte>>?)null, null);

    /// <summary>
    /// Tries to read a scalar value by UTF-8 key.
    /// </summary>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <param name="value">UTF-8 value bytes on success.</param>
    /// <returns>True when a scalar exists under <paramref name="key"/>.</returns>
    public bool TryGetScalar(ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
    {
        var name = Encoding.UTF8.GetString(key);
        if (_scalars.TryGetValue(name, out var bytes))
        {
            value = bytes.Span;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Returns the section items for <paramref name="key"/>, or an empty
    /// array when the key is missing or empty.
    /// </summary>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <returns>Section items.</returns>
    public TemplateData[] GetSection(ReadOnlySpan<byte> key)
    {
        var name = Encoding.UTF8.GetString(key);
        return _sections.GetValueOrDefault(name, EmptySections);
    }

    /// <summary>True when <paramref name="key"/> resolves to a non-empty section or a non-empty scalar.</summary>
    /// <param name="key">UTF-8 key bytes.</param>
    /// <returns>Truthiness for Mustache section semantics.</returns>
    public bool IsTruthy(ReadOnlySpan<byte> key)
    {
        var name = Encoding.UTF8.GetString(key);
        if (_sections.TryGetValue(name, out var items) && items.Length > 0)
        {
            return true;
        }

        return _scalars.TryGetValue(name, out var bytes) && bytes.Length > 0;
    }
}
