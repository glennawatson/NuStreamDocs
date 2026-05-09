// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Common page-shell knobs shared by the built-in themes.
/// </summary>
public interface IThemeShellOptions
{
    /// <summary>Gets the UTF-8 top-bar site title injected into every page.</summary>
    byte[] SiteName { get; }

    /// <summary>Gets the UTF-8 href for the site logo image (e.g. <c>images/logo.png</c>); empty to fall back to the site name.</summary>
    byte[] Logo => [];

    /// <summary>Gets the UTF-8 absolute site URL (e.g. <c>https://reactiveui.net</c>); empty when no canonical / OpenGraph URL should be rendered.</summary>
    byte[] SiteUrl { get; }

    /// <summary>Gets the UTF-8 HTML <c>lang</c> attribute value.</summary>
    byte[] Language { get; }

    /// <summary>Gets the UTF-8 footer copyright line.</summary>
    byte[] Copyright { get; }

    /// <summary>Gets the UTF-8 raw-HTML footer copyright block; emitted verbatim when non-empty and overrides the plain-text <see cref="Copyright"/> rendering.</summary>
    byte[] CopyrightHtml => [];

    /// <summary>Gets the footer social-link list rendered after the copyright block; empty when no social links should appear.</summary>
    ThemeSocialLink[] SocialLinks => [];

    /// <summary>Gets the UTF-8 path bytes (relative to project root) of an HTML partial whose contents replace the entire footer-meta inner block.</summary>
    byte[] FooterPartialPath => [];

    /// <summary>Gets the UTF-8 canonical repository URL; empty when no source link should be rendered.</summary>
    byte[] RepoUrl { get; }

    /// <summary>Gets the UTF-8 repo-relative edit prefix; empty when no edit link should be rendered.</summary>
    byte[] EditUri { get; }

    /// <summary>Gets the UTF-8 href used for the page favicon; empty when no favicon link should be rendered.</summary>
    byte[] Favicon { get; }

    /// <summary>Gets the asset-relative path of the default favicon (e.g. <c>/images/favicon.svg</c>); empty when the theme ships no default.</summary>
    byte[] DefaultEmbeddedFaviconRelativeUrl => [];

    /// <summary>Gets the asset-relative URL of the theme's primary stylesheet; empty when the theme has none.</summary>
    byte[] PrimaryStylesheetRelativeUrl => [];

    /// <summary>Gets a value indicating whether to render a scroll-to-top button.</summary>
    bool EnableScrollToTop { get; }

    /// <summary>Gets a value indicating whether to enable scroll-spy on the page TOC.</summary>
    bool EnableTocFollow { get; }

    /// <summary>Gets a value indicating whether to render prev/next footer links.</summary>
    bool EnableNavigationFooter { get; }

    /// <summary>Gets a value indicating whether footer neighbours should stay within the current section.</summary>
    bool SectionScopedFooter { get; }

    /// <summary>Gets a value indicating whether the plugin should emit the bundled static assets.</summary>
    bool WriteEmbeddedAssets { get; }

    /// <summary>Gets the UTF-8 URL prefix the page template should use for asset references.</summary>
    /// <returns>The active asset-root prefix.</returns>
    ReadOnlySpan<byte> ResolveAssetRoot();
}
