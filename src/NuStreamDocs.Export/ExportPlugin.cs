// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuStreamDocs.Common;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Export;

/// <summary>Harvests rendered documentation chapters across a build and emits a consolidated print/PDF-ready <c>/print.html</c> bundle.</summary>
[System.Diagnostics.DebuggerDisplay("ExportPlugin: {Name}")]
public sealed class ExportPlugin : IBuildConfigurePlugin, IPageScanPlugin, IBuildFinalizePlugin
{
    /// <summary>Thread-safe per-build chapter queue.</summary>
    private readonly ConcurrentQueue<ExportPageEntry> _chapters = [];

    /// <summary>Configured options.</summary>
    private readonly ExportOptions _options;

    /// <summary>Directory-URL mode flag captured during configure.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Initializes a new instance of the <see cref="ExportPlugin"/> class with default options.</summary>
    public ExportPlugin()
        : this(ExportOptions.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ExportPlugin"/> class.</summary>
    /// <param name="options">Export options.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
    public ExportPlugin(ExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "export"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority ScanPriority => new(PluginBand.Late);

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        _useDirectoryUrls = context.UseDirectoryUrls;
        _chapters.Clear();
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Scan(in PageScanContext context)
    {
        if (context.RelativePath.IsEmpty)
        {
            return;
        }

        var servedUrl = Utf8MarkdownUrl.FromRelativePath(context.RelativePath, _useDirectoryUrls);
        var entry = ExportHtmlExtractor.Extract(context.RelativePath.Value, servedUrl, context.Html);
        _chapters.Enqueue(entry);
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        if (_chapters.IsEmpty || context.OutputRoot.IsEmpty)
        {
            return;
        }

        ExportPageEntry[] snapshot = [.. _chapters];
        Array.Sort(snapshot, CompareEntries);

        var bytes = ExportDocumentWriter.BuildDocument(_options, snapshot);
        var outPath = context.OutputRoot.File(_options.OutputFileName);
        _ = outPath.Directory.Create();
        await outPath.WriteAllBytesAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Orders <c>index.md</c> first, followed by ordinal path comparison.</summary>
    /// <param name="left">Left entry.</param>
    /// <param name="right">Right entry.</param>
    /// <returns>Signed comparison value.</returns>
    internal static int CompareEntries(ExportPageEntry left, ExportPageEntry right)
    {
        var leftIsRoot = string.Equals(left.SortKey, "index.md", StringComparison.OrdinalIgnoreCase);
        var rightIsRoot = string.Equals(right.SortKey, "index.md", StringComparison.OrdinalIgnoreCase);
        if (leftIsRoot != rightIsRoot)
        {
            return leftIsRoot ? -1 : 1;
        }

        return string.CompareOrdinal(left.SortKey, right.SortKey);
    }
}
