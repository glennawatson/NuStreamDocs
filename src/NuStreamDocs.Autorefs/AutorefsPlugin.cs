// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Autorefs.Logging;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Autorefs;

/// <summary>
/// Plugin that collects heading anchor IDs during render and rewrites
/// <c>@autoref:ID</c> markers in the emitted HTML at finalize.
/// </summary>
/// <remarks>
/// Theme plugins, API-generator plugins, and citation plugins all
/// share one <see cref="AutorefsRegistry"/> via the plugin's
/// <see cref="Registry"/> property, so cross-document references
/// resolve no matter which plugin produced the destination.
/// </remarks>
public sealed class AutorefsPlugin : IDocPlugin, IMarkdownPreprocessor
{
    /// <summary>Length of the <c>.md</c> extension, dropped when computing the served URL.</summary>
    private const int MarkdownExtensionLength = 3;

    /// <summary>Logger used during the resolution pass.</summary>
    private readonly ILogger _logger;

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

    /// <summary>Gets the shared registry. Other plugins may publish IDs into it during configure or render.</summary>
    public AutorefsRegistry Registry { get; }

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _ = context;
        _ = cancellationToken;

        // Watch-mode rebuilds reuse the plugin instance — drop the previous build's registrations before any
        // OnRenderPageAsync hook fires. Other plugins register from their per-page hook (which runs strictly
        // after OnConfigureAsync), so this clear is safe regardless of registration order.
        Registry.Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var pageUrlBytes = ToPageUrlBytes(context.RelativePath);
        HeadingIdScanner.ScanAndRegister(context.Html.WrittenSpan, pageUrlBytes, Registry);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public bool NeedsRewrite(ReadOnlySpan<byte> source) =>
        AutorefsReferenceLinkPreprocessor.NeedsRewrite(source);

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer) =>
        AutorefsReferenceLinkPreprocessor.Rewrite(source, writer);

    /// <inheritdoc/>
    public void Preprocess(ReadOnlySpan<byte> source, IBufferWriter<byte> writer, FilePath relativePath)
    {
        _ = relativePath;
        AutorefsReferenceLinkPreprocessor.Rewrite(source, writer);
    }

    /// <inheritdoc/>
    public ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        AutorefsLoggingHelper.LogResolutionStart(_logger, Registry.Count);
        var (resolved, missing) = AutorefsRewriter.RewriteAll(context.OutputRoot, Registry, _logger);
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
        ReadOnlySpan<byte> suffix = hasMd ? ".html"u8 : default;

        var size = Encoding.UTF8.GetByteCount(sourceChars) + suffix.Length;
        var result = new byte[size];
        var written = Encoding.UTF8.GetBytes(sourceChars, result);
        suffix.CopyTo(result.AsSpan(written));
        return result;
    }
}
