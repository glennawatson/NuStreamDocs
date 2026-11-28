// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Llms;

/// <summary>
/// Collects rendered pages during the build and emits <c>llms.txt</c>, optional
/// <c>llms-full.txt</c>, and optional clean <c>.md</c> page siblings for AI coding
/// assistants and search tools.
/// </summary>
[System.Diagnostics.DebuggerDisplay("LlmsPlugin: {Name}")]
public sealed class LlmsPlugin : IBuildConfigurePlugin, IPageScanPlugin, IBuildFinalizePlugin, IHeadExtraProvider
{
    /// <summary>Collected page entries across parallel page scans.</summary>
    private readonly ConcurrentQueue<LlmsPageEntry> _entries = [];

    /// <summary>Configured options.</summary>
    private readonly LlmsOptions _options;

    /// <summary>Normalized site URL ending with <c>/</c>, or <c>null</c> when unconfigured.</summary>
    private byte[]? _baseUrlBytes;

    /// <summary>Captured UTF-8 site name.</summary>
    private byte[] _siteName = [];

    /// <summary>Whether directory URLs are enabled.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="LlmsPlugin"/> class with <see cref="LlmsOptions.Default"/>.</summary>
    public LlmsPlugin()
        : this(LlmsOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="LlmsPlugin"/> class.</summary>
    /// <param name="options">Plugin configuration.</param>
    public LlmsPlugin(in LlmsOptions options) => _options = options;

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "llms"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority ScanPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <summary>Gets the head link advertising <c>/llms.txt</c>.</summary>
    private static ReadOnlySpan<byte> HeadLinkBytes =>
        "<link rel=\"alternate\" type=\"text/markdown\" href=\"/llms.txt\" title=\"LLMs Index\">\n"u8;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _entries.Clear();
        _baseUrlBytes = LlmsWriter.NormalizeBaseUrl(context.SiteUrl);
        _siteName = context.SiteName;
        _useDirectoryUrls = context.UseDirectoryUrls;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Scan(in PageScanContext context)
    {
        if (!LlmsMarkdownExtractor.TryCreateEntry(
            context.RelativePath,
            context.Source,
            _useDirectoryUrls,
            _options,
            out var entry))
        {
            return;
        }

        _entries.Enqueue(entry);
    }

    /// <inheritdoc/>
    public ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        if (_entries.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        LlmsPageEntry[] snapshot = [.. _entries];
        Array.Sort(snapshot, LlmsWriter.CompareEntries);
        LlmsWriter.WriteOutputs(context.OutputRoot, _options, _siteName, _baseUrlBytes, snapshot);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        if (!_options.EmitHeadLink)
        {
            return;
        }

        writer.Write(HeadLinkBytes);
    }
}
