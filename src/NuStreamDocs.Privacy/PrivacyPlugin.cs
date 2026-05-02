// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Privacy.Logging;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Privacy plugin. Detects external <c>http(s)://</c> assets
/// referenced from rendered pages, rewrites the references to local
/// paths under <see cref="PrivacyOptions.AssetDirectory"/>, and
/// downloads each unique URL once at finalize time.
/// </summary>
/// <remarks>
/// Per-page <see cref="OnRenderPageAsync"/> rewrites the page HTML in
/// place; URL-to-local-path mappings accumulate in a thread-safe
/// registry. <see cref="OnFinalizeAsync"/> drives the actual HTTP fetches
/// in parallel. Already-downloaded assets (cache hits on subsequent
/// builds) skip the network entirely.
/// <para>
/// In <see cref="PrivacyOptions.AuditOnly"/> mode the plugin records
/// the external URLs it would have rewritten without touching the
/// HTML or hitting the network — useful for compliance review on
/// production sites.
/// </para>
/// </remarks>
public sealed class PrivacyPlugin : IDocPlugin
{
    /// <summary>Configured option set; captured at registration time.</summary>
    private readonly PrivacyOptions _options;

    /// <summary>Combined allow/skip-list filter; built once at construction.</summary>
    private readonly HostFilter _filter;

    /// <summary>URL → local-path registry shared across worker threads.</summary>
    private readonly ExternalAssetRegistry _registry;

    /// <summary>Audit-mode bag of detected external URL bytes; populated only when <see cref="PrivacyOptions.AuditOnly"/> is on.</summary>
    private readonly ConcurrentDictionary<byte[], byte> _auditedUrls = new(ByteArrayComparer.Instance);

    /// <summary>Inline style hashes accumulated when <see cref="PrivacyOptions.GenerateCspManifest"/> is on.</summary>
    private readonly ConcurrentDictionary<string, byte> _styleHashes = new(StringComparer.Ordinal);

    /// <summary>Inline script hashes accumulated when <see cref="PrivacyOptions.GenerateCspManifest"/> is on.</summary>
    private readonly ConcurrentDictionary<string, byte> _scriptHashes = new(StringComparer.Ordinal);

    /// <summary>Logger captured at construction; defaults to <see cref="NullLogger.Instance"/> when no logger is supplied.</summary>
    private readonly ILogger _logger;

    /// <summary>Output root captured during <see cref="OnConfigureAsync"/>.</summary>
    private string _outputRoot = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="PrivacyPlugin"/> class with default options.</summary>
    public PrivacyPlugin()
        : this(PrivacyOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PrivacyPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public PrivacyPlugin(in PrivacyOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PrivacyPlugin"/> class with a logger.</summary>
    /// <param name="options">Plugin options.</param>
    /// <param name="logger">Logger to receive privacy diagnostics.</param>
    public PrivacyPlugin(in PrivacyOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _options = options;
        _filter = new(options.HostsToSkip, options.HostsAllowed, options.UrlIncludePatterns, options.UrlExcludePatterns);
        _registry = new(options.AssetDirectory is [] ? PrivacyOptions.Default.AssetDirectory : options.AssetDirectory);
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Name => "privacy";

    /// <summary>Gets the snapshot of external URLs the plugin has seen so far. Populated on every build; in audit mode it's the only output, in normal mode it's a side-channel for tooling.</summary>
    public string[] AuditedUrls =>
        GetAuditedUrlsSnapshot();

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _outputRoot = context.OutputRoot;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (!_options.Enabled)
        {
            return ValueTask.CompletedTask;
        }

        var html = context.Html;
        var rendered = html.WrittenSpan;
        var hasUrls = ExternalUrlScanner.MayHaveExternalUrls(rendered);
        var hasLinks = ExternalLinkPolisher.MayHaveExternalLinks(rendered);
        var hasInlineBlocks = _options.GenerateCspManifest && CspHashCollector.MayHaveInlineBlocks(rendered);
        if (!hasUrls && !hasLinks && !hasInlineBlocks)
        {
            return ValueTask.CompletedTask;
        }

        if (hasInlineBlocks)
        {
            CspHashCollector.Collect(rendered, _styleHashes, _scriptHashes);
        }

        if (_options.AuditOnly)
        {
            ExternalUrlScanner.Audit(rendered, _filter, _auditedUrls);
            return ValueTask.CompletedTask;
        }

        // Snapshot the rendered span into a pooled buffer, reset the writer,
        // and let the rewriter write the new HTML straight into the page
        // sink — saves the intermediate byte[] the previous shape allocated
        // on every page that carried an external URL.
        HtmlSnapshotRewriter.Rewrite(html, this, static (snapshot, dst, self) =>
        {
            if (PrivacyRewriter.TryRewriteInto(snapshot, self._options, self._registry, self._filter, dst))
            {
                return;
            }

            // No pass changed the input — restore the original bytes verbatim.
            dst.Write(snapshot);
        });

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        var root = _outputRoot is [] ? context.OutputRoot : _outputRoot;
        if (!_options.Enabled || root is [])
        {
            return;
        }

        var registrySnapshot = _registry.UrlsSnapshot();
        PrivacyLoggingHelper.LogLocalizationStart(_logger, registrySnapshot.Length, root);

        var cacheRoot = ResolveCacheRoot(root);
        var settings = new ExternalAssetDownloader.DownloadSettings(
            _options.DownloadParallelism,
            _options.DownloadTimeout,
            _options.MaxRetries);
        var failures = _options.AuditOnly
            ? (string[])[]
            : await ExternalAssetDownloader
                .DownloadAllAsync(_registry, root, cacheRoot, settings, _filter, _logger, cancellationToken)
                .ConfigureAwait(false);

        WriteManifestIfRequested(root);
        WriteCspManifestIfRequested(root);
        PrivacyLoggingHelper.LogFinalizeSummary(_logger, registrySnapshot.Length - failures.Length, failures.Length, cachedCount: 0);
        ThrowIfRequested(failures);
    }

    /// <summary>Writes the keys of <paramref name="hashes"/> as a sorted JSON array.</summary>
    /// <param name="json">JSON writer.</param>
    /// <param name="hashes">Concurrent hash set.</param>
    private static void WriteHashArray(Utf8JsonWriter json, ConcurrentDictionary<string, byte> hashes)
    {
        json.WriteStartArray();
        string[] ordered = [.. hashes.Keys];
        Array.Sort(ordered, StringComparer.Ordinal);
        for (var i = 0; i < ordered.Length; i++)
        {
            json.WriteStringValue(ordered[i]);
        }

        json.WriteEndArray();
    }

    /// <summary>Throws <see cref="PrivacyDownloadException"/> when <see cref="PrivacyOptions.FailOnError"/> is set and any download failed.</summary>
    /// <param name="failures">Failed-URL list returned by the downloader.</param>
    private void ThrowIfRequested(string[] failures)
    {
        if (!_options.FailOnError || failures.Length is 0)
        {
            return;
        }

        throw new PrivacyDownloadException(failures);
    }

    /// <summary>Writes the audit manifest to <see cref="PrivacyOptions.AuditManifestPath"/> under <paramref name="outputRoot"/> when the option is non-empty.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    private void WriteManifestIfRequested(string outputRoot)
    {
        var path = _options.AuditManifestPath;
        if (path is [])
        {
            return;
        }

        var target = Path.Combine(outputRoot, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using var stream = File.Create(target);
        using var json = new Utf8JsonWriter(stream, new() { Indented = true });
        json.WriteStartObject();
        json.WriteBoolean("auditOnly"u8, _options.AuditOnly);
        json.WritePropertyName("urls"u8);
        json.WriteStartArray();
        var auditedUrls = GetAuditedUrlsSnapshot();
        for (var i = 0; i < auditedUrls.Length; i++)
        {
            json.WriteStringValue(auditedUrls[i]);
        }

        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
        PrivacyLoggingHelper.LogAuditWrite(_logger, target, auditedUrls.Length);
    }

    /// <summary>Writes the CSP-hash manifest when <see cref="PrivacyOptions.GenerateCspManifest"/> is set and a path is configured.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    private void WriteCspManifestIfRequested(string outputRoot)
    {
        if (!_options.GenerateCspManifest || _options.CspManifestPath is [])
        {
            return;
        }

        var target = Path.Combine(outputRoot, _options.CspManifestPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using var stream = File.Create(target);
        using var json = new Utf8JsonWriter(stream, new() { Indented = true });
        json.WriteStartObject();
        json.WritePropertyName("styleSrc"u8);
        WriteHashArray(json, _styleHashes);
        json.WritePropertyName("scriptSrc"u8);
        WriteHashArray(json, _scriptHashes);
        json.WriteEndObject();
    }

    /// <summary>Resolves the absolute cache root, falling back to <c>{outputRoot}/.cache/privacy</c> when the option is empty.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <returns>Absolute cache directory.</returns>
    private string ResolveCacheRoot(string outputRoot) =>
        _options.CacheDirectory is []
            ? Path.Combine(outputRoot, ".cache", "privacy")
            : _options.CacheDirectory;

    /// <summary>Builds a snapshot of the audited URLs, decoding from byte storage at the diagnostic boundary.</summary>
    /// <returns>Right-sized URL array.</returns>
    private string[] GetAuditedUrlsSnapshot()
    {
        if (!_options.AuditOnly)
        {
            return _registry.UrlsSnapshot();
        }

        byte[][] keys = [.. _auditedUrls.Keys];
        var result = new string[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            result[i] = Encoding.UTF8.GetString(keys[i]);
        }

        return result;
    }
}
