// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Config;

/// <summary>
/// Subset of mkdocs.yml fields the renderer consumes.
/// </summary>
/// <remarks>
/// Kept deliberately small; we read what affects layout
/// (site name, nav, theme palette/features) and ignore the rest.
/// New fields are added as emitter features grow.
/// <para>
/// <see cref="Nav"/> is a plain <see cref="NavEntry"/>[] rather than
/// an immutable collection: the renderer treats it as immutable by
/// convention, and we avoid the per-element overhead of
/// <c>ImmutableArray</c>'s wrapping struct in hot lookup paths.
/// </para>
/// </remarks>
/// <param name="SiteName">Top-bar/site title.</param>
/// <param name="SiteUrl">Canonical site URL (optional).</param>
/// <param name="ThemeName">Theme identifier (e.g. <c>material</c>, <c>zensical</c>).</param>
/// <param name="Nav">Flat list of nav entries; nested groups will be added later.</param>
/// <param name="UseDirectoryUrls">When true, pages emit as <c>foo/index.html</c> and links resolve to <c>foo/</c>; matches mkdocs' default behavior.</param>
public readonly record struct MkDocsConfig(
    string SiteName,
    string? SiteUrl,
    string ThemeName,
    NavEntry[] Nav,
    bool UseDirectoryUrls = true);
