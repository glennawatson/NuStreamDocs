// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Autorefs.Logging;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Plugin that rewrites mkdocs-autorefs reference-link shapes into the
/// <c>@autoref:</c> URL marker, scans rendered pages for heading anchor
/// IDs, and substitutes the resolved URLs after the cross-page barrier.
/// </summary>
/// <remarks>
/// Theme plugins, API-generator plugins, and citation plugins all
/// share one <see cref="AutorefsRegistry"/> via the plugin's
/// <see cref="Registry"/> property, so cross-document references
/// resolve no matter which plugin produced the destination.
/// </remarks>
public sealed class AutorefsPlugin
    : IBuildConfigurePlugin,
      IPagePreRenderPlugin,
      IPageScanPlugin,
      IBuildResolvePlugin,
      IPagePostResolvePlugin,
      IBuildFinalizePlugin
{
    /// <summary>Length of the <c>.md</c> extension, dropped when computing the served URL.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Logger used during the resolution pass.</summary>
    private readonly ILogger _logger;

    /// <summary>Resolved / missing counters accumulated across the post-resolve hook for the final summary log.</summary>
    private long _resolvedCount;

    /// <summary>Unresolved-marker counter accumulated across the post-resolve hook.</summary>
    private long _missingCount;

    /// <summary>Initializes a new instance of the <see cref="AutorefsPlugin"/> class.</summary>
    public AutorefsPlugin()
        : this(new(), NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AutorefsPlugin"/> class with a shared registry.</summary>
    /// <param name="registry">Registry to publish into and resolve from.</param>
    public AutorefsPlugin(AutorefsRegistry registry)
        : this(registry, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AutorefsPlugin"/> class with a registry and logger.</summary>
    /// <param name="registry">Registry to publish into and resolve from.</param>
    /// <param name="logger">Logger.</param>
    public AutorefsPlugin(AutorefsRegistry registry, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(logger);
        Registry = registry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "autorefs"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PreRenderPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority ScanPriority => new(PluginBand.Late);

    /// <inheritdoc/>
    public PluginPriority ResolvePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority PostResolvePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <summary>Gets the shared registry. Other plugins may publish IDs into it during configure or render.</summary>
    public AutorefsRegistry Registry { get; }

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;

        // Watch-mode rebuilds reuse the plugin instance — drop the previous build's registrations before any
        // Scan hook fires. Other plugins register from their per-page hook (which runs strictly
        // after ConfigureAsync), so this clear is safe regardless of registration order.
        Registry.Clear();
        Interlocked.Exchange(ref _resolvedCount, 0);
        Interlocked.Exchange(ref _missingCount, 0);

        // Register the @autoref: marker so the engine's cross-page fast-path skips pages without it.
        context.CrossPageMarkers.Register([.. AutorefScanner.Marker]);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        AutorefsReferenceLinkPreprocessor.NeedsRewrite(source);

    /// <inheritdoc/>
    public void PreRender(in PagePreRenderContext context) =>
        AutorefsReferenceLinkPreprocessor.Rewrite(context.Source, context.Output);

    /// <inheritdoc/>
    public void Scan(in PageScanContext context)
    {
        var pageUrlBytes = ToPageUrlBytes(context.RelativePath);
        HeadingIdScanner.ScanAndRegister(context.Html, pageUrlBytes, Registry);
    }

    /// <inheritdoc/>
    public ValueTask ResolveAsync(BuildResolveContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;

        AutorefsLoggingHelper.LogResolutionStart(_logger, Registry.Count);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    bool IPagePostResolvePlugin.NeedsRewrite(ReadOnlySpan<byte> html) =>
        html.IndexOf(AutorefScanner.Marker) >= 0;

    /// <inheritdoc/>
    public void Rewrite(in PagePostResolveContext context)
    {
        var sourcePage = context.RelativePath.FileName;
        var totals = AutorefsRewriter.RewriteSpan(context.Html, Registry, context.Output, _logger, sourcePage);
        Interlocked.Add(ref _resolvedCount, totals.Resolved);
        Interlocked.Add(ref _missingCount, totals.Missing);
    }

    /// <inheritdoc/>
    public ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;

        var resolved = (int)Interlocked.Read(ref _resolvedCount);
        var missing = (int)Interlocked.Read(ref _missingCount);
        AutorefsLoggingHelper.LogResolutionComplete(_logger, resolved, missing);
        return ValueTask.CompletedTask;
    }

    /// <summary>Maps a source-relative <c>.md</c> path to the served-page URL as UTF-8 bytes (suffix swapped to <c>.html</c>).</summary>
    /// <param name="relativePath">Source-relative markdown path, forward-slashed.</param>
    /// <returns>Page-relative URL bytes.</returns>
    private static byte[] ToPageUrlBytes(FilePath relativePath)
    {
        var path = relativePath.AsSpan();
        var hasMd = path.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        var sourceChars = hasMd ? path[..^MarkdownExtensionLength] : path;
        var suffix = hasMd ? ".html"u8 : default;

        var size = Encoding.UTF8.GetByteCount(sourceChars) + suffix.Length;
        var result = new byte[size];
        var written = Encoding.UTF8.GetBytes(sourceChars, result);
        suffix.CopyTo(result.AsSpan(written));
        return result;
    }
}
