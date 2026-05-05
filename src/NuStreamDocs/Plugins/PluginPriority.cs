// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Plugins;

/// <summary>
/// Bid a plugin places to determine its position within a phase.
/// </summary>
/// <param name="Band">Coarse ordering band; the dominant sort key.</param>
/// <param name="Tiebreak">Secondary sort key used to disambiguate two plugins that share a band; lower runs first.</param>
public readonly record struct PluginPriority(PluginBand Band, int Tiebreak = 0) : IComparable<PluginPriority>, IComparable
{
    /// <summary>Gets the default <see cref="PluginBand.Normal"/> priority with no tiebreak.</summary>
    public static PluginPriority Normal => new(PluginBand.Normal);

    /// <summary>Returns true when <paramref name="left"/> sorts before <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>True when <paramref name="left"/> sorts before <paramref name="right"/>.</returns>
    public static bool operator <(PluginPriority left, PluginPriority right) => left.CompareTo(right) < 0;

    /// <summary>Returns true when <paramref name="left"/> sorts after <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>True when <paramref name="left"/> sorts after <paramref name="right"/>.</returns>
    public static bool operator >(PluginPriority left, PluginPriority right) => left.CompareTo(right) > 0;

    /// <summary>Returns true when <paramref name="left"/> sorts at or before <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>True when <paramref name="left"/> sorts at or before <paramref name="right"/>.</returns>
    public static bool operator <=(PluginPriority left, PluginPriority right) => left.CompareTo(right) <= 0;

    /// <summary>Returns true when <paramref name="left"/> sorts at or after <paramref name="right"/>.</summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>True when <paramref name="left"/> sorts at or after <paramref name="right"/>.</returns>
    public static bool operator >=(PluginPriority left, PluginPriority right) => left.CompareTo(right) >= 0;

    /// <inheritdoc/>
    public int CompareTo(PluginPriority other)
    {
        var bandDelta = (int)Band - (int)other.Band;
        return bandDelta is not 0 ? bandDelta : Tiebreak - other.Tiebreak;
    }

    /// <inheritdoc/>
    public int CompareTo(object? obj) => obj is PluginPriority other
        ? CompareTo(other)
        : throw new ArgumentException("Object is not a PluginPriority.", nameof(obj));
}
