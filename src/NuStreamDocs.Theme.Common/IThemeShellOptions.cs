// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Common;

/// <summary>
/// Common page-shell knobs shared by the built-in themes.
/// </summary>
public interface IThemeShellOptions
{
    /// <summary>Gets the top-bar site title injected into every page.</summary>
    string SiteName { get; }

    /// <summary>Gets the HTML <c>lang</c> attribute value.</summary>
    string Language { get; }

    /// <summary>Gets the footer copyright line.</summary>
    string Copyright { get; }

    /// <summary>Gets the canonical repository URL; empty when no source link should be rendered.</summary>
    string RepoUrl { get; }

    /// <summary>Gets the repo-relative edit prefix; empty when no edit link should be rendered.</summary>
    string EditUri { get; }

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

    /// <summary>Gets the URL prefix the page template should use for asset references.</summary>
    /// <returns>The active asset-root prefix.</returns>
    string ResolveAssetRoot();
}
