// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace NuStreamDocs.Caching;

/// <summary>
/// Internal helper that builds a path-keyed
/// <see cref="FrozenDictionary{TKey,TValue}"/> from a
/// <see cref="ManifestEntry"/> array.
/// </summary>
/// <remarks>
/// Frozen dictionaries are read-mostly and outperform
/// <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>
/// during the parallel render stage where every worker probes the
/// index. Keyed by ordinal-string so paths compare exactly as written
/// — culture-aware comparison is wrong for file paths.
/// </remarks>
internal static class ManifestIndex
{
    /// <summary>Builds the frozen index over <paramref name="entries"/>.</summary>
    /// <param name="entries">Source entries.</param>
    /// <returns>The frozen, ordinal-keyed lookup.</returns>
    internal static FrozenDictionary<string, ManifestEntry> Build(ManifestEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0)
        {
            return FrozenDictionary<string, ManifestEntry>.Empty;
        }

        var working = new Dictionary<string, ManifestEntry>(entries.Length, StringComparer.Ordinal);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            working[entry.RelativePath] = entry;
        }

        return working.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
