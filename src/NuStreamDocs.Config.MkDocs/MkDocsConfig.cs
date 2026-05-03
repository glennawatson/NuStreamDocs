// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config.MkDocs;

/// <summary>
/// Subset of mkdocs.yml fields the renderer consumes (site-level metadata only).
/// </summary>
/// <remarks>
/// Nav-tree integration is owned by <c>NuStreamDocs.Nav.NavOptions.CuratedEntries</c>; reader
/// assemblies populate it via fluent extensions (<c>NavOptions.FromMkDocsYaml(...)</c>,
/// <c>NavOptions.FromDocFxTocs(...)</c>) so the core <c>MkDocsConfig</c> stays a flat
/// site-metadata bag and dialect-specific nav serialization never leaks into core.
/// </remarks>
/// <param name="SiteName">Top-bar/site title.</param>
/// <param name="SiteUrl">Canonical site URL (optional).</param>
/// <param name="ThemeName">Theme identifier (e.g. <c>material</c>, <c>zensical</c>).</param>
/// <param name="UseDirectoryUrls">When true, pages emit as <c>foo/index.html</c> and links resolve to <c>foo/</c>; matches mkdocs' default behavior.</param>
/// <param name="SiteAuthor">Site-wide author name surfaced through the rendered <c>&lt;meta name="author"&gt;</c> tag (optional).</param>
public readonly record struct MkDocsConfig(
    string SiteName,
    string? SiteUrl,
    string ThemeName,
    bool UseDirectoryUrls = true,
    string? SiteAuthor = null);
