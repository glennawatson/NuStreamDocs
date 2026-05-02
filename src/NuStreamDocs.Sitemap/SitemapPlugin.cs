// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Sitemap;

/// <summary>
/// Sitemap plugin. Collects every rendered page's URL during the
/// build and writes <c>sitemap.xml</c> + <c>robots.txt</c> to the
/// output root in <see cref="OnFinalizeAsync"/>.
/// </summary>
/// <remarks>
/// Requires <c>site_url</c> in the config — without it the URLs in
/// the sitemap can't be made absolute, so the plugin no-ops with a
/// note in the build log. <c>robots.txt</c> is emitted alongside
/// pointing crawlers at the sitemap URL.
/// </remarks>
public sealed class SitemapPlugin : IDocPlugin
{
    /// <summary>Per-page entries collected during the build; drained at finalize time.</summary>
    private readonly ConcurrentQueue<string> _entries = [];

    /// <summary>Resolved canonical site URL (with trailing slash) captured at configure time; null when missing.</summary>
    private string? _baseUrl;

    /// <inheritdoc/>
    public string Name => "sitemap";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _baseUrl = NormalizeBaseUrl(context.Config.SiteUrl);
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var url = SitemapWriter.RelativePathToUrlPath(context.RelativePath);
        if (url.Length is 0)
        {
            return ValueTask.CompletedTask;
        }

        _entries.Enqueue(url);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_baseUrl is null || _entries.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        string[] snapshot = [.. _entries];
        Array.Sort(snapshot, StringComparer.Ordinal);
        SitemapWriter.WriteSitemap(context.OutputRoot, _baseUrl, snapshot);
        SitemapWriter.WriteRobots(context.OutputRoot, _baseUrl);

        return ValueTask.CompletedTask;
    }

    /// <summary>Normalizes <paramref name="raw"/> to an absolute base URL with a trailing slash, or returns <c>null</c> when unusable.</summary>
    /// <param name="raw">The configured <c>site_url</c>.</param>
    /// <returns>Normalized base URL or <c>null</c>.</returns>
    private static string? NormalizeBaseUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.EndsWith('/') ? raw : raw + '/';
    }
}
