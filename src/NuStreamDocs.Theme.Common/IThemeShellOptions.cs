// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Common page-shell knobs shared by the built-in themes.
/// </summary>
/// <remarks>
/// Scalar text fields (site name, language, copyright, repo / edit URLs) are stored as UTF-8
/// bytes per the project's byte-first pipeline rule — the page-shell template flows pure UTF-8
/// from <c>OnConfigureAsync</c> through every render. String-shaped construction goes through
/// each theme's option-extension class (<c>WithSiteName</c>, <c>WithCopyright</c>, …), which
/// encodes once at the boundary.
/// </remarks>
public interface IThemeShellOptions
{
    /// <summary>Gets the UTF-8 top-bar site title injected into every page.</summary>
    byte[] SiteName { get; }

    /// <summary>Gets the UTF-8 absolute site URL (e.g. <c>https://reactiveui.net</c>); empty when no canonical / OpenGraph URL should be rendered. Mirrors mkdocs's <c>site_url</c> config.</summary>
    byte[] SiteUrl { get; }

    /// <summary>Gets the UTF-8 HTML <c>lang</c> attribute value.</summary>
    byte[] Language { get; }

    /// <summary>Gets the UTF-8 footer copyright line.</summary>
    byte[] Copyright { get; }

    /// <summary>Gets the UTF-8 canonical repository URL; empty when no source link should be rendered.</summary>
    byte[] RepoUrl { get; }

    /// <summary>Gets the UTF-8 repo-relative edit prefix; empty when no edit link should be rendered.</summary>
    byte[] EditUri { get; }

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
    byte[] ResolveAssetRoot();
}
