// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material3;

/// <summary>Configuration for <see cref="Material3ThemePlugin"/>.</summary>
/// <remarks>
/// Scalar text fields are stored as UTF-8 bytes per the project's byte-first pipeline rule.
/// String-shaped construction goes through <c>Material3ThemeOptionsExtensions</c>'s
/// <c>WithXxx</c> helpers, which encode once at the boundary.
/// </remarks>
/// <param name="AssetSource">Where the page template loads CSS / JS from.</param>
/// <param name="EmbeddedAssetRoot">UTF-8 local URL prefix when assets are bundled.</param>
/// <param name="CdnRoot">UTF-8 remote URL prefix when assets are served from a CDN.</param>
/// <param name="SiteName">UTF-8 top-bar site title injected into every page.</param>
/// <param name="SiteUrl">UTF-8 absolute site URL (e.g. <c>https://reactiveui.net</c>); empty when no canonical / og:url should be rendered. Mirrors mkdocs's <c>site_url</c>.</param>
/// <param name="Language">UTF-8 HTML <c>lang</c> attribute value.</param>
/// <param name="Copyright">UTF-8 footer copyright line.</param>
/// <param name="RepoUrl">UTF-8 canonical repository URL; empty when no source link should be rendered.</param>
/// <param name="EditUri">UTF-8 path inside the repo that prefixes a page's relative path to form an edit URL; empty when no edit link should be rendered.</param>
/// <param name="Favicon">UTF-8 href used for the page favicon (e.g. <c>./images/favicons/favicon.ico</c>); empty when no favicon link should be rendered.</param>
/// <param name="EnableScrollToTop">Render a scroll-to-top button (mkdocs-material's <c>navigation.top</c>).</param>
/// <param name="EnableTocFollow">Enable scroll-spy on the page TOC (mkdocs-material's <c>toc.follow</c>).</param>
/// <param name="EnableNavigationFooter">Render prev/next page links in the page footer (mkdocs-material's <c>navigation.footer</c>).</param>
/// <param name="SectionScopedFooter">When true, prev/next stop at the closest enclosing section instead
/// of crossing siblings; only applies when <see cref="EnableNavigationFooter"/> is true.</param>
public readonly record struct Material3ThemeOptions(
    Material3AssetSource AssetSource,
    byte[] EmbeddedAssetRoot,
    byte[] CdnRoot,
    byte[] SiteName,
    byte[] SiteUrl,
    byte[] Language,
    byte[] Copyright,
    byte[] RepoUrl,
    byte[] EditUri,
    byte[] Favicon,
    bool EnableScrollToTop,
    bool EnableTocFollow,
    bool EnableNavigationFooter,
    bool SectionScopedFooter) : IThemeShellOptions
{
    /// <summary>Gets the default embedded-asset root URL prefix.</summary>
    public static byte[] DefaultEmbeddedAssetRoot { get; } = [.. "/assets"u8];

    /// <summary>Gets the option set with all defaults populated.</summary>
    public static Material3ThemeOptions Default { get; } = new(
        AssetSource: Material3AssetSource.Embedded,
        EmbeddedAssetRoot: DefaultEmbeddedAssetRoot,
        CdnRoot: [],
        SiteName: [],
        SiteUrl: [],
        Language: [.. "en"u8],
        Copyright: [],
        RepoUrl: [],
        EditUri: [],
        Favicon: [],
        EnableScrollToTop: true,
        EnableTocFollow: true,
        EnableNavigationFooter: true,
        SectionScopedFooter: false);

    /// <inheritdoc/>
    public bool WriteEmbeddedAssets => AssetSource == Material3AssetSource.Embedded;

    /// <summary>Resolves the asset-root URL according to <see cref="AssetSource"/>.</summary>
    /// <returns>The active UTF-8 asset-root prefix.</returns>
    public byte[] ResolveAssetRoot() =>
        AssetSource == Material3AssetSource.Cdn ? CdnRoot : EmbeddedAssetRoot;
}
