// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Optional contract for plugins publishing a linear page order (typically a navigation builder).
/// Theme plugins use it to render prev/next links in the page footer. Implementations should serve
/// reads from a precomputed lookup so per-page calls stay sub-microsecond.
/// </summary>
public interface INavNeighboursProvider
{
    /// <summary>Looks up the previous and next pages in the global linear nav order.</summary>
    /// <param name="relativePath">Source-relative path of the current page.</param>
    /// <returns>The neighbours; <see cref="NavNeighbours.None"/> when the page is not in the nav.</returns>
    NavNeighbours GetNeighbours(in FilePath relativePath);

    /// <summary>Looks up the previous and next pages within the closest section that contains <paramref name="relativePath"/>; never crosses out of that section.</summary>
    /// <param name="relativePath">Source-relative path of the current page.</param>
    /// <returns>The section-scoped neighbours; <see cref="NavNeighbours.None"/> when the page is not in the nav or sits at a section boundary with no in-section neighbour on a side.</returns>
    NavNeighbours GetSectionNeighbours(in FilePath relativePath);

    /// <summary>
    /// Returns true when the primary sidebar would have nothing useful to render for
    /// <paramref name="relativePath"/> (e.g. a top-level leaf page with no descendants).
    /// Default: false.
    /// </summary>
    /// <param name="relativePath">Source-relative path of the current page.</param>
    /// <returns>True when the sidebar should be hidden for the page.</returns>
    bool ShouldHidePrimarySidebar(in FilePath relativePath) => false;
}
