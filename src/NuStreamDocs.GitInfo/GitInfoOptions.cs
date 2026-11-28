// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.GitInfo;

/// <summary>Configuration for <see cref="GitInfoPlugin"/>.</summary>
/// <param name="EmitFooter">When <c>true</c>, appends a revision-date and contributor footer inside rendered pages.</param>
/// <param name="EmitContributors">When <c>true</c>, includes contributor names alongside the revision date.</param>
/// <param name="EmitMetaTag">When <c>true</c>, injects <c>&lt;meta name="dcterms.modified"&gt;</c> into <c>&lt;head&gt;</c>.</param>
/// <param name="EnrichSitemap">When <c>true</c>, adds <c>&lt;lastmod&gt;</c> elements to <c>sitemap.xml</c> when present.</param>
/// <param name="IncludeStylesheet">When <c>true</c>, emits and links the bundled <c>git-info.css</c> stylesheet.</param>
/// <param name="MaxContributors">Maximum number of distinct contributors shown per page.</param>
/// <param name="LogProvider">Optional custom <see cref="IGitLogProvider"/>; <c>null</c> uses <see cref="ProcessGitLogProvider.Instance"/>.</param>
[System.Diagnostics.DebuggerDisplay("GitInfoOptions: Footer={EmitFooter}, MaxContributors={MaxContributors}")]
public readonly record struct GitInfoOptions(
    bool EmitFooter,
    bool EmitContributors,
    bool EmitMetaTag,
    bool EnrichSitemap,
    bool IncludeStylesheet,
    int MaxContributors,
    IGitLogProvider? LogProvider)
{
    /// <summary>Default contributor display cap.</summary>
    private const int DefaultContributorCap = 5;

    /// <summary>Gets the default maximum contributor count.</summary>
    public static int DefaultMaxContributors => DefaultContributorCap;

    /// <summary>Gets the default options: footer, contributors, meta tag, sitemap enrichment, and stylesheet enabled.</summary>
    public static GitInfoOptions Default { get; } = new(
        true,
        true,
        true,
        true,
        true,
        DefaultContributorCap,
        null);
}
