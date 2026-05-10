// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Sitemap;

/// <summary>
/// Collects every rendered page's URL during the build and writes
/// <c>sitemap.xml</c> plus <c>robots.txt</c> to the output root.
/// Requires <c>site_url</c> in the config; otherwise no-ops.
/// </summary>
public sealed class SitemapPlugin : IBuildConfigurePlugin, IPageScanPlugin, IBuildFinalizePlugin
{
    /// <summary>Collected UTF-8 URL bytes per page.</summary>
    private readonly ConcurrentQueue<byte[]> _entries = [];

    /// <summary>Site URL bytes with trailing slash; null when not configured.</summary>
    private byte[]? _baseUrlBytes;

    /// <summary>Build-pipeline directory-URL flag captured at configure time.</summary>
    private bool _useDirectoryUrls;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "sitemap"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority ScanPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _baseUrlBytes = NormalizeBaseUrl(context.SiteUrl);
        _useDirectoryUrls = context.UseDirectoryUrls;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Scan(in PageScanContext context)
    {
        var url = Utf8MarkdownUrl.FromRelativePath(context.RelativePath, _useDirectoryUrls);
        if (url.Length is 0)
        {
            return;
        }

        _entries.Enqueue(url);
    }

    /// <inheritdoc/>
    public ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
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
