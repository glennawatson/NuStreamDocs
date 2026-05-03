// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Common;
using NuStreamDocs.Logging;
using NuStreamDocs.Plugins;
using NuStreamDocs.Search.Logging;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Search;

/// <summary>
/// Search-index plugin. Gathers per-page text during render, writes
/// the chosen format at finalize, and contributes a discovery
/// <c>&lt;meta&gt;</c> tag to <c>&lt;head&gt;</c> so theme JS knows
/// where to fetch the index.
/// </summary>
/// <remarks>
/// Theme-agnostic. Pair with <c>UseMaterialTheme</c> +
/// <see cref="SearchFormat.Lunr"/> to feed mkdocs-material's bundled
/// JS, or with <c>UseMaterial3Theme</c> + the default
/// <see cref="SearchFormat.Pagefind"/> to scale to large corpora.
/// </remarks>
public sealed class SearchPlugin(SearchOptions options, ILogger logger) : IDocPlugin, IHeadExtraProvider
{
    /// <summary>Configured option set.</summary>
    private readonly SearchOptions _options = options;

    /// <summary>Per-page documents collected during the parallel render stage.</summary>
    private readonly ConcurrentBag<SearchDocument> _documents = [];

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>Alias for <see cref="SearchOptions.SearchableFrontmatterKeys"/> — the option list is already byte-shaped, so the per-page extractor reads directly without copying.</summary>
    private readonly byte[][] _searchableFrontmatterKeyBytes = options.SearchableFrontmatterKeys;

    /// <summary>Output root captured during configure.</summary>
    private string _outputRoot = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="SearchPlugin"/> class with default options.</summary>
    public SearchPlugin()
        : this(SearchOptions.Default, NullLogger.Instance)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SearchPlugin"/> class.</summary>
    /// <param name="options">Plugin options.</param>
    public SearchPlugin(in SearchOptions options)
        : this(options, NullLogger.Instance)
    {
    }

    /// <inheritdoc/>
    public string Name => "search";

    /// <inheritdoc/>
    public ValueTask OnConfigureAsync(PluginConfigureContext context, CancellationToken cancellationToken)
    {
        _outputRoot = context.OutputRoot;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask OnRenderPageAsync(PluginRenderContext context, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var html = context.Html.WrittenSpan;
        if (html.IsEmpty)
        {
            return ValueTask.CompletedTask;
        }

        using var textRental = PageBuilderPool.Rent(html.Length / 2);
        var textBuffer = textRental.Writer;
        var titleBytes = HtmlTextExtractor.Extract(html, textBuffer);
        var url = ToHtmlUrl(context.RelativePath);
        if (titleBytes.Length == 0)
        {
            var fallback = Path.GetFileNameWithoutExtension(context.RelativePath);
            titleBytes = Encoding.UTF8.GetBytes(fallback);
        }

        if (_searchableFrontmatterKeyBytes.Length > 0)
        {
            FrontmatterValueExtractor.AppendKeysTo(context.Source.Span, _searchableFrontmatterKeyBytes, textBuffer);
        }

        _documents.Add(new(url, titleBytes, [.. textBuffer.WrittenSpan]));
        LogInvokerHelper.Invoke(_logger, LogLevel.Debug, url, textBuffer.WrittenCount, static (l, u, len) => SearchLoggingHelper.LogDocumentIndexed(l, u, len));

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask OnFinalizeAsync(PluginFinalizeContext context, CancellationToken cancellationToken)
    {
        var root = _outputRoot is [] ? context.OutputRoot : _outputRoot;
        if (root is [])
        {
            return;
        }

        var searchRoot = Path.Combine(root, _options.OutputSubdirectory);
        var docs = FilterAndSort(_documents, _options.MinTokenLength);
        SearchLoggingHelper.LogIndexBuildStart(_logger, docs.Length, _options.Format, searchRoot);
        string primaryIndexPath;
        switch (_options.Format)
        {
            case SearchFormat.Lunr:
            {
                Directory.CreateDirectory(searchRoot);
                primaryIndexPath = Path.Combine(searchRoot, "search_index.json");
                LunrIndexWriter.Write(primaryIndexPath, _options.Language, docs, _options.ExtraStopwords);
                break;
            }

            default:
            {
                // Pagefind is the default format; any new SearchFormat
                // values not handled above fall back to Pagefind too.
                PagefindIndexWriter.Write(searchRoot, docs);
                primaryIndexPath = Path.Combine(searchRoot, "pagefind-entry.json");
                break;
            }
        }

        if (_options.Compression is not SearchCompression.None && File.Exists(primaryIndexPath))
        {
            await EmitCompressedSiblingsAsync(primaryIndexPath, _options.Compression, cancellationToken).ConfigureAwait(false);
        }

        var totalBytes = TotalContentBytes(docs);
        SearchLoggingHelper.LogIndexBuildComplete(_logger, docs.Length, totalBytes, searchRoot);
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var manifestPath = _options.Format switch
        {
            SearchFormat.Lunr => "/" + _options.OutputSubdirectory + "/search_index.json",
            _ => "/" + _options.OutputSubdirectory + "/pagefind-entry.json",
        };

        HeadExtraWriter.WriteUtf8(writer, "<meta name=\"nustreamdocs:search-format\" content=\""u8);
        HeadExtraWriter.WriteString(writer, FormatName(_options.Format));
        HeadExtraWriter.WriteUtf8(writer, "\">\n"u8);
        HeadExtraWriter.WriteUtf8(writer, "<meta name=\"nustreamdocs:search-index\" content=\""u8);
        HeadExtraWriter.WriteString(writer, manifestPath);
        HeadExtraWriter.WriteUtf8(writer, "\">\n"u8);
    }

    /// <summary>Snapshots the documents harvested so far; exposed for tests.</summary>
    /// <returns>A fresh array copy of the bag's contents.</returns>
    internal SearchDocument[] DocumentsSnapshot() => [.. _documents];

    /// <summary>Emits <c>.gz</c> (and <c>.br</c> when <see cref="SearchCompression.Smallest"/>) siblings of the JSON index for CDN <c>Content-Encoding</c> negotiation.</summary>
    /// <param name="path">Absolute path to the just-written JSON index.</param>
    /// <param name="compression">Compression knob; <see cref="SearchCompression.None"/> is a no-op handled by the caller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when both siblings have been written.</returns>
    private static async Task EmitCompressedSiblingsAsync(string path, SearchCompression compression, CancellationToken cancellationToken)
    {
        var raw = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        await WriteGzipAsync(path + ".gz", raw, cancellationToken).ConfigureAwait(false);
        if (compression is not SearchCompression.Smallest)
        {
            return;
        }

        await WriteBrotliAsync(path + ".br", raw, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes <paramref name="raw"/> through <see cref="GZipStream"/> at the smallest compression level.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="raw">Bytes to compress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    private static async Task WriteGzipAsync(string path, byte[] raw, CancellationToken cancellationToken)
    {
        await using var output = File.Create(path);
        await using var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: false);
        await gzip.WriteAsync(raw, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Writes <paramref name="raw"/> through <see cref="BrotliStream"/> at the smallest compression level.</summary>
    /// <param name="path">Absolute output path.</param>
    /// <param name="raw">Bytes to compress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the file has been written.</returns>
    private static async Task WriteBrotliAsync(string path, byte[] raw, CancellationToken cancellationToken)
    {
        await using var output = File.Create(path);
        await using var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: false);
        await brotli.WriteAsync(raw, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Filters out documents shorter than <paramref name="minTokenLength"/> + sorts deterministically.</summary>
    /// <param name="bag">Concurrent bag of harvested docs.</param>
    /// <param name="minTokenLength">Minimum text length to keep.</param>
    /// <returns>A right-sized, ordinal-sorted array.</returns>
    private static SearchDocument[] FilterAndSort(ConcurrentBag<SearchDocument> bag, int minTokenLength)
    {
        var min = Math.Max(0, minTokenLength);
        var docs = new List<SearchDocument>(bag.Count);
        foreach (var doc in bag)
        {
            if (doc.Text.Length >= min)
            {
                docs.Add(doc);
            }
        }

        docs.Sort(static (a, b) => string.CompareOrdinal(a.RelativeUrl, b.RelativeUrl));
        return [.. docs];
    }

    /// <summary>Translates a source-relative markdown path to its rendered HTML URL.</summary>
    /// <param name="markdownPath">Source-relative path (e.g. <c>guide/intro.md</c>).</param>
    /// <returns>Site-relative URL with <c>.html</c> extension.</returns>
    private static string ToHtmlUrl(string markdownPath)
    {
        if (markdownPath is [])
        {
            return string.Empty;
        }

        var withoutExt = Path.ChangeExtension(markdownPath, ".html");
        return withoutExt.Replace('\\', '/');
    }

    /// <summary>Returns the lowercase format name for the discovery <c>&lt;meta&gt;</c> tag.</summary>
    /// <param name="format">Format selection.</param>
    /// <returns>Format name string suitable for a meta-tag value.</returns>
    private static string FormatName(SearchFormat format) => format switch
    {
        SearchFormat.Lunr => "lunr",
        _ => "pagefind",
    };

    /// <summary>Sums the UTF-8 byte length across every indexed document.</summary>
    /// <param name="docs">Documents to measure.</param>
    /// <returns>Total content bytes.</returns>
    private static long TotalContentBytes(SearchDocument[] docs)
    {
        long total = 0;
        for (var i = 0; i < docs.Length; i++)
        {
            total += docs[i].Text.Length;
        }

        return total;
    }
}
