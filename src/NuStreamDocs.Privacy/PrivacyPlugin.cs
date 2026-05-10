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
/// Detects external <c>http(s)://</c> assets in rendered pages, rewrites them to local paths
/// under <see cref="PrivacyOptions.AssetDirectory"/>, and downloads each unique URL once at
/// finalize time. Audit mode records the URLs without rewriting or downloading.
/// </summary>
public sealed class PrivacyPlugin : IBuildConfigurePlugin, IPagePostRenderPlugin, IBuildFinalizePlugin
{
    /// <summary>PostRender tiebreak ordering Privacy after the theme shell wrap and Nav.</summary>
    private const int PostRenderTiebreak = 3;

    /// <summary>Configured option set; captured at registration time.</summary>
    private readonly PrivacyOptions _options;

    /// <summary>Combined allow/skip-list filter; built once at construction.</summary>
    private readonly HostFilter _filter;

    /// <summary>URL → local-path registry shared across worker threads.</summary>
    private readonly ExternalAssetRegistry _registry;

    /// <summary>Audit-mode bag of detected external URL bytes; populated only when <see cref="PrivacyOptions.AuditOnly"/> is on.</summary>
    private readonly ConcurrentDictionary<byte[], byte> _auditedUrls = new(ByteArrayComparer.Instance);

    /// <summary>Inline style hashes accumulated when <see cref="PrivacyOptions.GenerateCspManifest"/> is on.</summary>
    private readonly ConcurrentDictionary<byte[], byte> _styleHashes = new(ByteArrayComparer.Instance);

    /// <summary>Inline script hashes accumulated when <see cref="PrivacyOptions.GenerateCspManifest"/> is on.</summary>
    private readonly ConcurrentDictionary<byte[], byte> _scriptHashes = new(ByteArrayComparer.Instance);

    /// <summary>Logger captured at construction; defaults to <see cref="NullLogger.Instance"/> when no logger is supplied.</summary>
    private readonly ILogger _logger;

    /// <summary>Output root captured during <see cref="ConfigureAsync"/>.</summary>
    private DirectoryPath _outputRoot;

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
        _options = options;
        _filter = new(options.HostsToSkip, options.HostsAllowed, options.UrlIncludePatterns, options.UrlExcludePatterns);
        var dirBytes = options.AssetDirectory is [] ? PrivacyOptions.Default.AssetDirectory : options.AssetDirectory;
        _registry = new(dirBytes);
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "privacy"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostRenderPriority => new(PluginBand.Latest, PostRenderTiebreak);

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => new(PluginBand.Late);

    /// <summary>Gets the snapshot of external URLs the plugin has seen as UTF-8 byte arrays.</summary>
    public byte[][] AuditedUrls => GetAuditedUrlBytesSnapshot();

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _outputRoot = context.OutputRoot;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> html)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        var hasUrls = ExternalUrlScanner.MayHaveExternalUrls(html);
        var hasLinks = ExternalLinkPolisher.MayHaveExternalLinks(html);
        var hasInlineBlocks = _options.GenerateCspManifest && CspHashCollector.MayHaveInlineBlocks(html);
        return hasUrls || hasLinks || hasInlineBlocks;
    }

    /// <inheritdoc/>
    public void PostRender(in PagePostRenderContext context)
    {
        var rendered = context.Html;
        var hasUrls = ExternalUrlScanner.MayHaveExternalUrls(rendered);
        var hasLinks = ExternalLinkPolisher.MayHaveExternalLinks(rendered);
        var hasInlineBlocks = _options.GenerateCspManifest && CspHashCollector.MayHaveInlineBlocks(rendered);

        if (hasInlineBlocks)
        {
            CspHashCollector.Collect(rendered, _styleHashes, _scriptHashes);
        }

        if (_options.AuditOnly)
        {
            ExternalUrlScanner.Audit(rendered, _filter, _auditedUrls);

            // Audit mode never mutates the HTML — pass the bytes through verbatim.
            context.Output.Write(rendered);
            return;
        }

        if (!hasUrls && !hasLinks)
        {
            // Inline-block CSP collection only — no HTML rewrite.
            context.Output.Write(rendered);
            return;
        }

        if (PrivacyRewriter.TryRewriteInto(rendered, _options, _registry, _filter, context.Output))
        {
            return;
        }

        // No pass changed the input — emit the original bytes verbatim.
        context.Output.Write(rendered);
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        var root = _outputRoot.IsEmpty ? context.OutputRoot : _outputRoot;
        if (!_options.Enabled || root.IsEmpty)
        {
            return;
        }

        var registeredCount = _registry.Count;
        PrivacyLoggingHelper.LogLocalizationStart(_logger, registeredCount, root.Value);

        var cacheRoot = ResolveCacheRoot(root);
        ExternalAssetDownloader.DownloadSettings settings = new(
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
        PrivacyLoggingHelper.LogFinalizeSummary(_logger, registeredCount - failures.Length, failures.Length, cachedCount: 0);
        ThrowIfRequested(failures);
    }

    /// <summary>Writes the keys of <paramref name="hashes"/> as a sorted JSON array.</summary>
    /// <param name="json">JSON writer.</param>
    /// <param name="hashes">Concurrent hash set.</param>
    private static void WriteHashArray(Utf8JsonWriter json, ConcurrentDictionary<byte[], byte> hashes)
    {
        json.WriteStartArray();
        byte[][] keys = [.. hashes.Keys];
        Array.Sort(keys, ByteArrayComparer.Instance);
        for (var i = 0; i < keys.Length; i++)
        {
            json.WriteStringValue(keys[i]);
        }

        json.WriteEndArray();
    }

    /// <summary>Decodes a UTF-8 relative output path and combines it under <paramref name="outputRoot"/>.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="pathBytes">Forward-slash relative path bytes.</param>
    /// <returns>Absolute output file path.</returns>
    private static FilePath ResolveOutputPath(in DirectoryPath outputRoot, ReadOnlySpan<byte> pathBytes)
    {
        Span<char> pathChars = stackalloc char[Encoding.UTF8.GetCharCount(pathBytes)];
        Encoding.UTF8.GetChars(pathBytes, pathChars);
        for (var i = 0; i < pathChars.Length; i++)
        {
            if (pathChars[i] is '/')
            {
                pathChars[i] = Path.DirectorySeparatorChar;
            }
        }

        return Path.Combine(outputRoot.Value, new(pathChars));
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
    private void WriteManifestIfRequested(in DirectoryPath outputRoot)
    {
        var pathBytes = _options.AuditManifestPath;
        if (pathBytes is [])
        {
            return;
        }

        var target = ResolveOutputPath(outputRoot, pathBytes);
        Directory.CreateDirectory(target.Directory.Value);

        using var stream = File.Create(target.Value);
        using Utf8JsonWriter json = new(stream, new() { Indented = true });
        json.WriteStartObject();
        json.WriteBoolean("auditOnly"u8, _options.AuditOnly);
        json.WritePropertyName("urls"u8);
        json.WriteStartArray();
        var auditedUrls = GetAuditedUrlBytesSnapshot();
        for (var i = 0; i < auditedUrls.Length; i++)
        {
            json.WriteStringValue(auditedUrls[i]);
        }

        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
        PrivacyLoggingHelper.LogAuditWrite(_logger, target.Value, auditedUrls.Length);
    }

    /// <summary>Writes the CSP-hash manifest when <see cref="PrivacyOptions.GenerateCspManifest"/> is set and a path is configured.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    private void WriteCspManifestIfRequested(in DirectoryPath outputRoot)
    {
        var pathBytes = _options.CspManifestPath;
        if (!_options.GenerateCspManifest || pathBytes is [])
        {
            return;
        }

        var target = ResolveOutputPath(outputRoot, pathBytes);
        Directory.CreateDirectory(target.Directory.Value);

        using var stream = File.Create(target.Value);
        using Utf8JsonWriter json = new(stream, new() { Indented = true });
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
    private DirectoryPath ResolveCacheRoot(in DirectoryPath outputRoot) =>
        _options.CacheDirectory is []
            ? (DirectoryPath)Path.Combine(outputRoot, ".cache", "privacy")
            : (DirectoryPath)Encoding.UTF8.GetString(_options.CacheDirectory);

    /// <summary>Builds a snapshot of the audited URLs as UTF-8 byte arrays.</summary>
    /// <returns>URL byte-array snapshot — registry contents in normal mode, audit-set contents in audit mode.</returns>
    private byte[][] GetAuditedUrlBytesSnapshot() =>
        _options.AuditOnly ? [.. _auditedUrls.Keys] : _registry.UrlsSnapshot();
}
