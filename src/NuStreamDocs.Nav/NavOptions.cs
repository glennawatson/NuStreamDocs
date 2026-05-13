// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Nav;

/// <summary>Configuration for <see cref="NavPlugin"/>.</summary>
/// <param name="Includes">Glob include patterns relative to the input root.</param>
/// <param name="Excludes">Glob exclude patterns; evaluated after <see cref="Includes"/>.</param>
/// <param name="HideEmptySections">When true, sections with no visible pages are dropped from the rendered nav.</param>
/// <param name="SortBy">Default ordering rule.</param>
/// <param name="Prune">When true, each page embeds only nodes on the active branch.</param>
/// <param name="Indexes">When true, an <c>index.md</c> / <c>README.md</c> inside a section folder is promoted to the section's landing page.</param>
/// <param name="WarnOnOrphanPages">When true, <c>.md</c> files included by the matcher but absent from the nav tree are logged.</param>
/// <param name="Tabs">When true, top-level nav entries render into the page header as a horizontal tab bar.</param>
/// <param name="CuratedEntries">When non-empty, the nav tree is built from this list instead of the directory walk; pages not listed here are still built but excluded from nav.</param>
/// <param name="UseDirectoryUrls">Directory-URL override; null defers to the pipeline's flag.</param>
/// <param name="HomeTab">When true (and <see cref="Tabs"/> is true), prepends a synthetic Home tab pointing at <c>/</c>.</param>
/// <param name="HomeTabLabel">UTF-8 label for the synthetic Home tab. Defaults to <c>Home</c>.</param>
public readonly record struct NavOptions(
    GlobPattern[] Includes,
    GlobPattern[] Excludes,
    bool HideEmptySections,
    NavSortBy SortBy,
    bool Prune,
    bool Indexes,
    bool WarnOnOrphanPages,
    bool Tabs,
    NavEntry[] CuratedEntries,
    bool? UseDirectoryUrls,
    bool HomeTab,
    byte[] HomeTabLabel)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static NavOptions Default { get; } = new(
        [],
        [],
        true,
        NavSortBy.FileName,
        false,
        true,
        true,
        false,
        [],
        null,
        true,
        [.. "Home"u8]);
}
