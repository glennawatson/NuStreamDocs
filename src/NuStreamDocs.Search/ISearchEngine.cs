// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search;

/// <summary>Per-engine backend contract used by <see cref="SearchPluginBase"/> to write the on-disk index.</summary>
public interface ISearchEngine
{
    /// <summary>Gets the lowercase format name surfaced via the <c>nustreamdocs:search-format</c> meta tag (e.g. <c>"pagefind"u8</c>, <c>"lunr"u8</c>).</summary>
    ReadOnlySpan<byte> FormatName { get; }

    /// <summary>Gets the manifest filename component (with leading <c>/</c>) appended to the search subdirectory; empty when the engine ships no fetchable manifest.</summary>
    ReadOnlySpan<byte> ManifestFileName { get; }

    /// <summary>Writes the engine-specific index files under <paramref name="searchRoot"/>.</summary>
    /// <param name="searchRoot">Absolute path to the per-engine output subdirectory; created by the caller.</param>
    /// <param name="documents">Documents harvested during render.</param>
    /// <returns>The path to the primary manifest file, or <see cref="FilePath.IsEmpty"/> when none was written.</returns>
    FilePath Write(DirectoryPath searchRoot, SearchDocument[] documents);
}
