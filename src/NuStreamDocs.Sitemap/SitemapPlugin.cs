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
    /// <summary>Per-page UTF-8 URL bytes collected during the build; drained at finalize time.</summary>
    private readonly ConcurrentQueue<byte[]> _entries = [];

    /// <summary>Resolved canonical site URL bytes (with trailing slash) captured at configure time; null when missing.</summary>
    private byte[]? _baseUrlBytes;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "sitemap"u8;

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _baseUrlBytes = NormalizeBaseUrl(context.SiteUrl);
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
        if (_baseUrlBytes is null || _entries.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        byte[][] snapshot = [.. _entries];
        Array.Sort(snapshot, static (a, b) => a.AsSpan().SequenceCompareTo(b));
        SitemapWriter.WriteSitemap(context.OutputRoot, _baseUrlBytes, snapshot);
        SitemapWriter.WriteRobots(context.OutputRoot, _baseUrlBytes);

        return ValueTask.CompletedTask;
    }

    /// <summary>Normalizes <paramref name="raw"/> to UTF-8 base-URL bytes with a trailing slash, or returns <c>null</c> when empty.</summary>
    /// <param name="raw">UTF-8 site-url bytes.</param>
    /// <returns>Normalized base-URL bytes or <c>null</c>.</returns>
    private static byte[]? NormalizeBaseUrl(ReadOnlySpan<byte> raw)
    {
        var trimmed = raw.Trim((byte)' ').Trim((byte)'\t');
        if (trimmed.IsEmpty)
        {
            return null;
        }

        if (trimmed[^1] is (byte)'/')
        {
            return trimmed.ToArray();
        }

        var dst = new byte[trimmed.Length + 1];
        trimmed.CopyTo(dst);
        dst[^1] = (byte)'/';
        return dst;
    }
}
