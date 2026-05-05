// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Plugins;

/// <summary>
/// Optional companion contract for plugins that publish a linear page
/// order (typically a navigation builder). Theme plugins use it to
/// render prev/next page links in the page footer (the equivalent of
/// mkdocs-material's <c>navigation.footer</c>).
/// </summary>
/// <remarks>
/// Theme plugins look up the first registered
/// <see cref="INavNeighboursProvider"/> during
/// <see cref="IBuildConfigurePlugin.ConfigureAsync"/> and call
/// <see cref="GetNeighbours(FilePath)"/> per page during render. The
/// provider is expected to be cheap (sub-microsecond): build any
/// linear index up front in your own <c>OnConfigure</c> and serve
/// reads from a <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>.
/// </remarks>
public interface INavNeighboursProvider
{
    /// <summary>Looks up the previous and next pages in the global linear nav order.</summary>
    /// <param name="relativePath">Source-relative path of the current page.</param>
    /// <returns>The neighbours; <see cref="NavNeighbours.None"/> when the page is not in the nav.</returns>
    NavNeighbours GetNeighbours(FilePath relativePath);

    /// <summary>Looks up the previous and next pages within the closest section that contains <paramref name="relativePath"/>; never crosses out of that section.</summary>
    /// <param name="relativePath">Source-relative path of the current page.</param>
    /// <returns>The section-scoped neighbours; <see cref="NavNeighbours.None"/> when the page is not in the nav or sits at a section boundary with no in-section neighbour on a side.</returns>
    NavNeighbours GetSectionNeighbours(FilePath relativePath);

    /// <summary>
    /// Returns true when the primary sidebar would have nothing useful to render for
    /// <paramref name="relativePath"/> — e.g. a top-level leaf page with no descendants.
    /// </summary>
    /// <param name="relativePath">Source-relative path of the current page.</param>
    /// <returns>True when the sidebar should be hidden for the page.</returns>
    /// <remarks>
    /// Default returns <see langword="false"/> so existing providers keep their current behaviour.
    /// </remarks>
    bool ShouldHidePrimarySidebar(FilePath relativePath) => false;
}
