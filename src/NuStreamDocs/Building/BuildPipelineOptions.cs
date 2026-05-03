// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Building;

/// <summary>Options bundle for the canonical <c>BuildPipeline.RunAsync</c> entry point.</summary>
/// <param name="Filter">Include/exclude glob filter applied during page discovery.</param>
/// <param name="Logger">Optional logger; <see langword="null"/> silences diagnostics.</param>
/// <param name="UseDirectoryUrls">When true, pages emit as <c>foo/index.html</c> instead of <c>foo.html</c>.</param>
/// <param name="IncludeDrafts">When true, pages whose frontmatter sets <c>draft: true</c> are emitted; when false they are skipped.</param>
/// <param name="SiteName">UTF-8 site name surfaced to plugins through <see cref="Plugins.PluginConfigureContext.SiteName"/>; empty when none configured.</param>
/// <param name="SiteUrl">UTF-8 canonical site URL surfaced to plugins through <see cref="Plugins.PluginConfigureContext.SiteUrl"/>; empty when none configured.</param>
/// <param name="SiteAuthor">UTF-8 site-wide author name surfaced to plugins through <see cref="Plugins.PluginConfigureContext.SiteAuthor"/>; empty when none configured.</param>
public readonly record struct BuildPipelineOptions(
    PathFilter Filter,
    ILogger? Logger,
    bool UseDirectoryUrls,
    bool IncludeDrafts,
    byte[] SiteName,
    byte[] SiteUrl,
    byte[] SiteAuthor)
{
    /// <summary>Gets the empty option set: no filter, no logger, flat URLs, drafts skipped, no site metadata.</summary>
    public static BuildPipelineOptions Default => new(PathFilter.Empty, null, false, false, [], [], []);
}
