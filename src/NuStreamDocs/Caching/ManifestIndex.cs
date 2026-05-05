// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Caching;

/// <summary>
/// Internal helper that builds a path-keyed
/// <see cref="Dictionary{TKey,TValue}"/> from a
/// <see cref="ManifestEntry"/> array.
/// </summary>
/// <remarks>
/// Keyed by ordinal-string so paths compare exactly as written —
/// culture-aware comparison is wrong for file paths.
/// </remarks>
internal static class ManifestIndex
{
    /// <summary>Builds the path-keyed index over <paramref name="entries"/>.</summary>
    /// <param name="entries">Source entries.</param>
    /// <returns>The ordinal-keyed lookup.</returns>
    internal static Dictionary<FilePath, ManifestEntry> Build(ManifestEntry[] entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Length == 0)
        {
            return EmptyCollections.DictionaryFor<FilePath, ManifestEntry>();
        }

        Dictionary<FilePath, ManifestEntry> working = new(entries.Length);
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            working[entry.RelativePath] = entry;
        }

        return working;
    }
}
