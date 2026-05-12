// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace NuStreamDocs.Redirects;

/// <summary>Configuration for <c>RedirectsPlugin</c>.</summary>
/// <param name="Redirects">Explicitly declared redirects (page-frontmatter <c>redirect_from</c> entries are merged in on top, with these winning on a conflict).</param>
/// <param name="Headers">Author-declared <c>_headers</c> rule blocks (appended after the defaults).</param>
/// <param name="EmitRedirectsFile">When true, write a <c>_redirects</c> file at the site root.</param>
/// <param name="EmitHeadersFile">When true, write a <c>_headers</c> file at the site root (only when there is at least one rule).</param>
/// <param name="EmitMetaRefreshPages">When true, write a meta-refresh HTML page at each redirect's source path (the host-agnostic fallback).</param>
/// <param name="ReadFrontmatterRedirects">When true, pick up <c>redirect_from</c> entries from page frontmatter.</param>
/// <param name="DefaultCacheHeaders">When true, prepend default <c>_headers</c> rules: a one-week cache for <c>/assets/*</c> and an immutable cache for <c>/assets/fonts/*</c>.</param>
/// <param name="FrontmatterKey">UTF-8 frontmatter key read for redirect sources.</param>
[SuppressMessage("Major Code Smell", "S107", Justification = "A flat options record; each field is an independent knob.")]
public readonly record struct RedirectsOptions(
    RedirectRule[] Redirects,
    HeaderRule[] Headers,
    bool EmitRedirectsFile,
    bool EmitHeadersFile,
    bool EmitMetaRefreshPages,
    bool ReadFrontmatterRedirects,
    bool DefaultCacheHeaders,
    byte[] FrontmatterKey)
{
    /// <summary>Gets the option set with all defaults populated.</summary>
    public static RedirectsOptions Default { get; } = new(
        Redirects: [],
        Headers: [],
        EmitRedirectsFile: true,
        EmitHeadersFile: true,
        EmitMetaRefreshPages: true,
        ReadFrontmatterRedirects: true,
        DefaultCacheHeaders: true,
        FrontmatterKey: [.. "redirect_from"u8]);
}
