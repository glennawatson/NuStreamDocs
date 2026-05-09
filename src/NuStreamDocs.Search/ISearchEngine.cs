// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Search;

/// <summary>
/// Backend contract every search engine implements. The base plugin
/// (<see cref="SearchPluginBase"/>) drives page scanning + manifest emission;
/// the engine plugs in the per-format details — what the manifest is called,
/// what the discovery <c>&lt;meta&gt;</c> tag should advertise, how to write
/// the index files.
/// </summary>
public interface ISearchEngine
{
    /// <summary>
    /// Gets the lowercase format name surfaced via the <c>nustreamdocs:search-format</c>
    /// meta tag for theme JS dispatch (e.g. <c>"pagefind"u8</c>, <c>"lunr"u8</c>).
    /// </summary>
    ReadOnlySpan<byte> FormatName { get; }

    /// <summary>
    /// Gets the filename component (with leading <c>/</c>) appended to the configured search
    /// subdirectory to build the manifest URL surfaced via the <c>nustreamdocs:search-index</c>
    /// meta tag (e.g. <c>"/search_index.json"u8</c> for Lunr). Empty for engines that don't ship
    /// a fetchable manifest — Pagefind, for example, ships a script loader the theme glue imports
    /// directly via the engine's head-extras instead.
    /// </summary>
    ReadOnlySpan<byte> ManifestFileName { get; }

    /// <summary>Writes the engine-specific index files under <paramref name="searchRoot"/>.</summary>
    /// <param name="searchRoot">Absolute path to the per-engine output subdirectory; created by the caller.</param>
    /// <param name="documents">Document corpus harvested during render.</param>
    /// <returns>The path to the primary manifest file. The caller emits <c>.gz</c>/<c>.br</c> sidecars off this when compression is enabled.</returns>
    FilePath Write(DirectoryPath searchRoot, SearchDocument[] documents);
}
