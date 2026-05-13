// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Redirects;

/// <summary>Fluent helpers for building <see cref="RedirectsOptions"/>.</summary>
public static class RedirectsOptionsExtensions
{
    /// <summary>Adds a permanent (301) redirect from <paramref name="from"/> to <paramref name="to"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="from">UTF-8 root-relative URL path to redirect from.</param>
    /// <param name="to">UTF-8 destination URL path (or absolute <c>https://…</c>).</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions Add(
        this in RedirectsOptions options,
        ReadOnlySpan<byte> from,
        ReadOnlySpan<byte> to) =>
        Add(options, from, to, true);

    /// <summary>Adds a redirect from <paramref name="from"/> to <paramref name="to"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="from">UTF-8 root-relative URL path to redirect from.</param>
    /// <param name="to">UTF-8 destination URL path (or absolute <c>https://…</c>).</param>
    /// <param name="permanent">True for a 301, false for a 302.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions Add(
        this in RedirectsOptions options,
        ReadOnlySpan<byte> from,
        ReadOnlySpan<byte> to,
        bool permanent) =>
        options with
        {
            Redirects = ArrayJoiner.Concat(options.Redirects, [new(from.ToArray(), to.ToArray(), permanent)])
        };

    /// <summary>Adds a <c>_headers</c> rule block: <paramref name="headerLines"/> applied to paths matching <paramref name="pathPattern"/>.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="pathPattern">UTF-8 path pattern (Netlify / Cloudflare-Pages syntax).</param>
    /// <param name="headerLines">UTF-8 header lines in <c>Name: value</c> form.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions AddHeaders(
        this in RedirectsOptions options,
        ReadOnlySpan<byte> pathPattern,
        params byte[][] headerLines) =>
        options with { Headers = ArrayJoiner.Concat(options.Headers, [new(pathPattern.ToArray(), headerLines)]) };

    /// <summary>Adds a <c>/*</c> rule with the uncontroversial security headers (<c>X-Content-Type-Options: nosniff</c> and <c>Referrer-Policy: strict-origin-when-cross-origin</c>).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions WithSecurityHeaders(this in RedirectsOptions options) =>
        AddHeaders(
            options,
            "/*"u8,
            [.. "X-Content-Type-Options: nosniff"u8],
            [.. "Referrer-Policy: strict-origin-when-cross-origin"u8]);

    /// <summary>Disables the default <c>_headers</c> cache rules.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions WithoutDefaultCacheHeaders(this in RedirectsOptions options) =>
        options with { DefaultCacheHeaders = false };

    /// <summary>Disables the per-redirect <c>&lt;meta http-equiv="refresh"&gt;</c> HTML pages (keeping only the <c>_redirects</c> file).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions WithoutMetaRefreshPages(this in RedirectsOptions options) =>
        options with { EmitMetaRefreshPages = false };

    /// <summary>Disables the <c>_redirects</c> file (keeping only the meta-refresh HTML pages).</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions WithoutRedirectsFile(this in RedirectsOptions options) =>
        options with { EmitRedirectsFile = false };

    /// <summary>Disables the <c>_headers</c> file.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions WithoutHeadersFile(this in RedirectsOptions options) =>
        options with { EmitHeadersFile = false };

    /// <summary>Disables reading <c>redirect_from</c> from page frontmatter.</summary>
    /// <param name="options">Source options.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions WithoutFrontmatterRedirects(this in RedirectsOptions options) =>
        options with { ReadFrontmatterRedirects = false };

    /// <summary>Replaces the frontmatter key read for redirect sources.</summary>
    /// <param name="options">Source options.</param>
    /// <param name="key">UTF-8 frontmatter key.</param>
    /// <returns>The updated options.</returns>
    public static RedirectsOptions WithFrontmatterKey(this in RedirectsOptions options, ReadOnlySpan<byte> key) =>
        options with { FrontmatterKey = key.ToArray() };
}
