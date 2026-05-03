// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material;

/// <summary>
/// Represents configuration options for the Material theme in the source documentation system.
/// </summary>
/// <remarks>
/// Scalar text fields are stored as UTF-8 bytes per the project's byte-first pipeline rule.
/// String-shaped construction goes through <c>MaterialThemeOptionsExtensions</c>'s <c>WithXxx</c>
/// helpers, which encode once at the boundary. The default <see cref="DefaultCdnRoot"/> /
/// <see cref="DefaultEmbeddedAssetRoot"/> properties remain string-shaped for human readability
/// in code; the byte forms are exposed via <see cref="DefaultCdnRootBytes"/> /
/// <see cref="DefaultEmbeddedAssetRootBytes"/>.
/// </remarks>
/// <param name="AssetSource">Gets or sets the source of the assets.</param>
/// <param name="EmbeddedAssetRoot">UTF-8 root path for embedded assets.</param>
/// <param name="CdnRoot">UTF-8 root URL for assets hosted on a CDN.</param>
/// <param name="SiteName">UTF-8 site title injected into every page.</param>
/// <param name="SiteUrl">UTF-8 absolute site URL (e.g. <c>https://reactiveui.net</c>); empty when no canonical / og:url should be rendered. Mirrors mkdocs's <c>site_url</c>.</param>
/// <param name="Language">UTF-8 HTML <c>lang</c> attribute value.</param>
/// <param name="Copyright">UTF-8 footer copyright line.</param>
/// <param name="RepoUrl">UTF-8 canonical repository URL (e.g. <c>https://github.com/owner/repo</c>); empty when no source link should be rendered.</param>
/// <param name="EditUri">UTF-8 path inside the repo that prefixes a page's relative path to form an edit URL (e.g. <c>edit/main/docs</c>); empty when no edit link should be rendered.</param>
/// <param name="EnableScrollToTop">Gets a value indicating whether to render a scroll-to-top button (the equivalent of mkdocs-material's <c>navigation.top</c>).</param>
/// <param name="EnableTocFollow">Gets a value indicating whether to enable scroll-spy on the page TOC (the equivalent of mkdocs-material's <c>toc.follow</c>).</param>
/// <param name="EnableNavigationFooter">Gets a value indicating whether to render prev/next page links in the page footer (the equivalent of mkdocs-material's <c>navigation.footer</c>).</param>
/// <param name="SectionScopedFooter">Gets a value indicating whether the prev/next links should stop at
/// the closest enclosing section instead of crossing siblings; only applies when
/// <see cref="EnableNavigationFooter"/> is true.</param>
public readonly record struct MaterialThemeOptions(
    MaterialAssetSource AssetSource,
    byte[] EmbeddedAssetRoot,
    byte[] CdnRoot,
    byte[] SiteName,
    byte[] SiteUrl,
    byte[] Language,
    byte[] Copyright,
    byte[] RepoUrl,
    byte[] EditUri,
    bool EnableScrollToTop,
    bool EnableTocFollow,
    bool EnableNavigationFooter,
    bool SectionScopedFooter) : IThemeShellOptions
{
    /// <summary>Gets the default jsdelivr CDN root for the upstream Material bundle this assembly is pinned to.</summary>
    /// <remarks>
    /// Resolves through jsdelivr's GitHub mirror because the
    /// <c>mkdocs-material</c> package on npm is a security placeholder
    /// — the real distribution lives on GitHub + PyPI. Pinned to the
    /// same Material build the lifted local bundle was taken from
    /// (<c>9.7.6</c>); bump in lockstep with any refresh of the files
    /// under <c>Templates/assets/</c>.
    /// </remarks>
    public static string DefaultCdnRoot =>
        "https://cdn.jsdelivr.net/gh/squidfunk/mkdocs-material@9.7.6/material/templates/assets";

    /// <summary>Gets the default embedded-asset root URL prefix.</summary>
    public static string DefaultEmbeddedAssetRoot => "/assets";

    /// <summary>Gets the default CDN root as UTF-8 bytes.</summary>
    public static byte[] DefaultCdnRootBytes { get; } =
        [.. "https://cdn.jsdelivr.net/gh/squidfunk/mkdocs-material@9.7.6/material/templates/assets"u8];

    /// <summary>Gets the default embedded-asset root as UTF-8 bytes.</summary>
    public static byte[] DefaultEmbeddedAssetRootBytes { get; } = [.. "/assets"u8];

    /// <summary>Gets the option set with all defaults populated.</summary>
    public static MaterialThemeOptions Default { get; } = new(
        AssetSource: MaterialAssetSource.Embedded,
        EmbeddedAssetRoot: DefaultEmbeddedAssetRootBytes,
        CdnRoot: DefaultCdnRootBytes,
        SiteName: [],
        SiteUrl: [],
        Language: [.. "en"u8],
        Copyright: [],
        RepoUrl: [],
        EditUri: [],
        EnableScrollToTop: true,
        EnableTocFollow: true,
        EnableNavigationFooter: true,
        SectionScopedFooter: false);

    /// <inheritdoc/>
    public bool WriteEmbeddedAssets => AssetSource == MaterialAssetSource.Embedded;

    /// <summary>Gets the UTF-8 URL prefix the page template should use for asset references.</summary>
    /// <returns>Either <see cref="EmbeddedAssetRoot"/> or <see cref="CdnRoot"/> depending on <see cref="AssetSource"/>.</returns>
    public byte[] ResolveAssetRoot() =>
        AssetSource == MaterialAssetSource.Cdn ? CdnRoot : EmbeddedAssetRoot;
}
