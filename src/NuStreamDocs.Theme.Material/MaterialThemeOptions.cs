// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Common;

namespace NuStreamDocs.Theme.Material;

/// <summary>
/// Represents configuration options for the Material theme in the source documentation system.
/// </summary>
/// <param name="AssetSource">Gets or sets the source of the assets.</param>
/// <param name="EmbeddedAssetRoot">Gets or sets the root path for embedded assets.</param>
/// <param name="CdnRoot">Gets or sets the root URL for assets hosted on a CDN.</param>
/// <param name="SiteName">Gets or sets the name of the site.</param>
/// <param name="Language">Gets or sets the language of the site.</param>
/// <param name="Copyright">Gets or sets the copyright information.</param>
/// <param name="RepoUrl">Gets the canonical repository URL (e.g. <c>https://github.com/owner/repo</c>); empty when no source link should be rendered.</param>
/// <param name="EditUri">Gets the path inside the repo that prefixes a page's relative path to form an edit URL (e.g. <c>edit/main/docs</c>); empty when no edit link should be rendered.</param>
/// <param name="EnableScrollToTop">Gets a value indicating whether to render a scroll-to-top button (the equivalent of mkdocs-material's <c>navigation.top</c>).</param>
/// <param name="EnableTocFollow">Gets a value indicating whether to enable scroll-spy on the page TOC (the equivalent of mkdocs-material's <c>toc.follow</c>).</param>
/// <param name="EnableNavigationFooter">Gets a value indicating whether to render prev/next page links in the page footer (the equivalent of mkdocs-material's <c>navigation.footer</c>).</param>
/// <param name="SectionScopedFooter">Gets a value indicating whether the prev/next links should stop at
/// the closest enclosing section instead of crossing siblings; only applies when
/// <see cref="EnableNavigationFooter"/> is true.</param>
public readonly record struct MaterialThemeOptions(
    MaterialAssetSource AssetSource,
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
    /// <summary>Gets the default jsdelivr CDN root for the upstream Material bundle this assembly is pinned to.</summary>
    /// <remarks>
    /// Resolves through jsdelivr's GitHub mirror because the
    /// <c>mkdocs-material</c> package on npm is a security placeholder
    /// — the real distribution lives on GitHub + PyPI. Pinned to the
    /// same Material build the lifted local bundle was taken from
    /// (<c>9.7.6</c>); bump in lockstep with any refresh of the files
    /// under <c>Templates/assets/</c>.
    /// <para>
    /// Note: upstream serves hashed filenames
    /// (<c>main.484c7ddc.min.css</c>, <c>palette.ab4e12ef.min.css</c>,
    /// <c>bundle.79ae519e.min.js</c>) while the local bundle uses
    /// stable names (<c>material.min.css</c>, …). Until the page
    /// template's asset filename map is mode-aware, the CDN mode
    /// requires the consumer to either host their own copy or
    /// override the asset paths. Tracked as a follow-up.
    /// </para>
    /// </remarks>
    public static string DefaultCdnRoot =>
        "https://cdn.jsdelivr.net/gh/squidfunk/mkdocs-material@9.7.6/material/templates/assets";

    /// <summary>Gets the option set with all defaults populated.</summary>
    public static MaterialThemeOptions Default { get; } = new(
        AssetSource: MaterialAssetSource.Embedded,
        EmbeddedAssetRoot: "assets",
        CdnRoot: DefaultCdnRoot,
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
    public bool WriteEmbeddedAssets => AssetSource == MaterialAssetSource.Embedded;

    /// <summary>Gets the URL prefix the page template should use for asset references.</summary>
    /// <returns>Either <see cref="EmbeddedAssetRoot"/> or <see cref="CdnRoot"/> depending on <see cref="AssetSource"/>.</returns>
    public string ResolveAssetRoot() =>
        AssetSource == MaterialAssetSource.Cdn ? CdnRoot : EmbeddedAssetRoot;
}
