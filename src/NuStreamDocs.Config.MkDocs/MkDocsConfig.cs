// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Subset of mkdocs.yml site-level metadata fields the renderer consumes.
/// </summary>
/// <param name="SiteName">Top-bar/site title.</param>
/// <param name="SiteUrl">Canonical site URL; optional.</param>
/// <param name="ThemeName">Theme identifier (e.g. <c>material</c>, <c>zensical</c>).</param>
/// <param name="UseDirectoryUrls">When true, pages emit as <c>foo/index.html</c> and links resolve to <c>foo/</c>.</param>
/// <param name="SiteAuthor">Site-wide author name written into the <c>&lt;meta name="author"&gt;</c> tag; optional.</param>
public readonly record struct MkDocsConfig(
    string SiteName,
    string? SiteUrl,
    string ThemeName,
    bool UseDirectoryUrls = true,
    string? SiteAuthor = null);
