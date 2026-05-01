// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav;

/// <summary>
/// Configuration for <see cref="NavPlugin"/>.
/// </summary>
/// <remarks>
/// Mirrors the most-used knobs of mkdocs nav. Defaults match
/// what large projects need out of the box; consumers tweak via
/// <c>builder.UseNav(opts =&gt; opts with { ... })</c>.
/// </remarks>
/// <param name="Includes">Glob include patterns relative to the input root.</param>
/// <param name="Excludes">Glob exclude patterns; evaluated after <see cref="Includes"/>.</param>
/// <param name="HideEmptySections">When true, sections with no visible pages are dropped from the rendered nav.</param>
/// <param name="SortBy">Default ordering rule.</param>
/// <param name="Prune">When true, each page only embeds the nodes on the active branch (mkdocs-material <c>navigation.prune</c>). Reduces emitted HTML size on large sites.</param>
/// <param name="Indexes">When true, an <c>index.md</c> (or <c>README.md</c>) inside a section folder is
/// promoted to the section's landing page and the section becomes clickable
/// (the equivalent of mkdocs-material's <c>navigation.indexes</c>).</param>
/// <param name="WarnOnOrphanPages">When true, every <c>.md</c> file under the input root that the matcher
/// would include but that doesn't appear in the rendered nav tree is reported via the logger
/// (mirrors the mkdocs <c>"pages exist in the docs directory, but are not included in the nav configuration"</c>
/// message). Default <see langword="true"/> — disable when generating partial trees on purpose.</param>
public readonly record struct NavOptions(
    string[] Includes,
    string[] Excludes,
    bool HideEmptySections,
    NavSortBy SortBy,
    bool Prune,
    bool Indexes,
    bool WarnOnOrphanPages)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static NavOptions Default { get; } = new(
        Includes: [],
        Excludes: [],
        HideEmptySections: true,
        SortBy: NavSortBy.FileName,
        Prune: false,
        Indexes: true,
        WarnOnOrphanPages: true);
}
