// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Sitemap;

/// <summary>
/// Configuration for <see cref="RedirectsPlugin"/>: where to read the
/// optional config file from, and whether to scan per-page frontmatter
/// for an alias list.
/// </summary>
/// <param name="ConfigFileName">
/// File name (relative to the input root) of the YAML redirect map;
/// <c>redirects.yml</c> by default.
/// </param>
/// <param name="LoadConfigFile">
/// When true (default), <see cref="RedirectsPlugin"/> reads
/// <see cref="ConfigFileName"/> at configure time and merges its
/// <c>from: to</c> entries into the redirect map.
/// </param>
/// <param name="ScanFrontmatterAliases">
/// When true (default), every page's frontmatter is scanned for an
/// <see cref="AliasFrontmatterKey"/> list, and each entry registers an
/// alias → page redirect.
/// </param>
/// <param name="AliasFrontmatterKey">
/// Frontmatter key to look up when <see cref="ScanFrontmatterAliases"/>
/// is enabled; defaults to <c>aliases</c>.
/// </param>
public readonly record struct RedirectsOptions(
    string ConfigFileName,
    bool LoadConfigFile,
    bool ScanFrontmatterAliases,
    string AliasFrontmatterKey)
{
    /// <summary>Gets the default-shaped options: read <c>redirects.yml</c> and scan <c>aliases:</c> in frontmatter.</summary>
    public static RedirectsOptions Default => new("redirects.yml", true, true, "aliases");
}
