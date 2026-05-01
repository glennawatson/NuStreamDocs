// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3;

/// <summary>Configuration for <see cref="Material3ThemePlugin"/>.</summary>
/// <param name="AssetSource">Where the page template loads CSS / JS from.</param>
/// <param name="EmbeddedAssetRoot">Local URL prefix when assets are bundled.</param>
/// <param name="CdnRoot">Remote URL prefix when assets are served from a CDN.</param>
/// <param name="SiteName">Top-bar site title injected into every page.</param>
/// <param name="Language">HTML <c>lang</c> attribute value.</param>
/// <param name="Copyright">Footer copyright line.</param>
/// <param name="RepoUrl">Canonical repository URL; empty when no source link should be rendered.</param>
/// <param name="EditUri">Path inside the repo that prefixes a page's relative path to form an edit URL; empty when no edit link should be rendered.</param>
/// <param name="EnableScrollToTop">Render a scroll-to-top button (mkdocs-material's <c>navigation.top</c>).</param>
/// <param name="EnableTocFollow">Enable scroll-spy on the page TOC (mkdocs-material's <c>toc.follow</c>).</param>
/// <param name="EnableNavigationFooter">Render prev/next page links in the page footer (mkdocs-material's <c>navigation.footer</c>).</param>
/// <param name="SectionScopedFooter">When true, prev/next stop at the closest enclosing section instead
/// of crossing siblings; only applies when <see cref="EnableNavigationFooter"/> is true.</param>
public readonly record struct Material3ThemeOptions(
    Material3AssetSource AssetSource,
    string EmbeddedAssetRoot,
    string CdnRoot,
    string SiteName,
    string Language,
    string Copyright,
    string RepoUrl,
    string EditUri,
    bool EnableScrollToTop,
    bool EnableTocFollow,
    bool EnableNavigationFooter,
    bool SectionScopedFooter) : IThemeShellOptions
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static Material3ThemeOptions Default { get; } = new(
        AssetSource: Material3AssetSource.Embedded,
        EmbeddedAssetRoot: "assets",
        CdnRoot: string.Empty,
        SiteName: string.Empty,
        Language: "en",
        Copyright: string.Empty,
        RepoUrl: string.Empty,
        EditUri: string.Empty,
        EnableScrollToTop: true,
        EnableTocFollow: true,
        EnableNavigationFooter: true,
        SectionScopedFooter: false);

    /// <inheritdoc/>
    public bool WriteEmbeddedAssets => AssetSource == Material3AssetSource.Embedded;

    /// <summary>Resolves the asset-root URL according to <see cref="AssetSource"/>.</summary>
    /// <returns>The active asset-root prefix.</returns>
    public string ResolveAssetRoot() =>
        AssetSource == Material3AssetSource.Cdn ? CdnRoot : EmbeddedAssetRoot;
}
