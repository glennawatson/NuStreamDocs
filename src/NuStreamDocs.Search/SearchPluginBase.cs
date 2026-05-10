// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Links;
using NuStreamDocs.Plugins;
using NuStreamDocs.Search.Logging;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Search;

/// <summary>Base class for engine-specific search plugins; drives page scan, finalize, and head-extra emission.</summary>
public abstract class SearchPluginBase : IBuildConfigurePlugin, IPageScanPlugin, IBuildFinalizePlugin, IHeadExtraProvider
{
    /// <summary>Engine implementation.</summary>
    private readonly ISearchEngine _engine;

    /// <summary>Documents collected during scan.</summary>
    private readonly ConcurrentBag<SearchDocument> _documents = [];

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Output root captured during configure.</summary>
    private DirectoryPath _outputRoot;

    /// <summary>Whether the site emits directory-style URLs.</summary>
    private bool _useDirectoryUrls;

    /// <summary>Pre-encoded manifest URL; empty when the engine has no fetchable manifest.</summary>
    private byte[]? _manifestUrlBytes;

    /// <summary>Initializes a new instance of the <see cref="SearchPluginBase"/> class.</summary>
    /// <param name="engine">Engine implementation that owns the on-disk format.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    protected SearchPluginBase(ISearchEngine engine, ILogger logger)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <inheritdoc/>
    public ReadOnlySpan<byte> Name => "search"u8;

    /// <inheritdoc/>
    public PluginPriority ConfigurePriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority ScanPriority => PluginPriority.Normal;

    /// <inheritdoc/>
    public PluginPriority FinalizePriority => PluginPriority.Normal;

    /// <summary>Gets the site-relative search subdirectory (e.g. <c>search</c>).</summary>
    protected abstract PathSegment OutputSubdirectory { get; }

    /// <summary>Gets the minimum text length to keep; shorter documents are dropped before write.</summary>
    protected abstract int MinTokenLength { get; }

    /// <summary>Gets the UTF-8 frontmatter keys whose values are folded into each page's searchable text. Empty for body-only indexing.</summary>
    protected abstract byte[][] SearchableFrontmatterKeys { get; }

    /// <summary>Gets UTF-8 <c>prefix:weight</c> pairs surfaced via the <c>nustreamdocs:search-section-priorities</c> meta tag. Empty disables the meta tag.</summary>
    protected abstract byte[] SectionPriorities { get; }

    /// <summary>Gets the path to the most recent index manifest written by the engine; <see cref="FilePath.IsEmpty"/> when none was written.</summary>
    protected FilePath PrimaryIndexPath { get; private set; }

    /// <inheritdoc/>
    public ValueTask ConfigureAsync(BuildConfigureContext context, CancellationToken cancellationToken)
    {
        _outputRoot = context.OutputRoot;
        _useDirectoryUrls = context.UseDirectoryUrls;
        _manifestUrlBytes = BuildManifestUrlBytes(OutputSubdirectory, _engine.ManifestFileName);
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Scan(in PageScanContext context)
    {
        var html = context.Html;
        if (html.IsEmpty)
        {
            return;
        }

        using var textRental = PageBuilderPool.Rent(html.Length / 2);
        var textBuffer = textRental.Writer;
        var titleBytes = HtmlTextExtractor.Extract(html, textBuffer);
        var url = ToHtmlUrl(context.RelativePath, _useDirectoryUrls);
        if (titleBytes.Length == 0)
        {
            // Slice the file-stem directly off the path span so the fallback
            // skips Path.GetFileNameWithoutExtension's intermediate string copy.
            var pathSpan = context.RelativePath.AsSpan();
            var lastSep = pathSpan.LastIndexOfAny('/', '\\');
            var fileSpan = lastSep < 0 ? pathSpan : pathSpan[(lastSep + 1)..];
            var dotIdx = fileSpan.LastIndexOf('.');
            var stemSpan = dotIdx < 0 ? fileSpan : fileSpan[..dotIdx];
            titleBytes = new byte[Encoding.UTF8.GetByteCount(stemSpan)];
            Encoding.UTF8.GetBytes(stemSpan, titleBytes);
        }

        var frontmatterKeys = SearchableFrontmatterKeys;
        if (frontmatterKeys.Length > 0)
        {
            FrontmatterValueExtractor.AppendKeysTo(context.Source, frontmatterKeys, textBuffer);
        }

        _documents.Add(new(url, titleBytes, [.. textBuffer.WrittenSpan]));
        SearchLoggingHelper.LogDocumentIndexed(_logger, url, textBuffer.WrittenCount);
    }

    /// <inheritdoc/>
    public async ValueTask FinalizeAsync(BuildFinalizeContext context, CancellationToken cancellationToken)
    {
        var root = _outputRoot.IsEmpty ? context.OutputRoot : _outputRoot;
        if (root.IsEmpty)
        {
            return;
        }

        DirectoryPath searchRoot = Path.Combine(root.Value, OutputSubdirectory);
        var docs = FilterAndSort(_documents, MinTokenLength);
        SearchLoggingHelper.LogIndexBuildStart(_logger, docs.Length, _engine.FormatName, searchRoot);
        searchRoot.Create();
        PrimaryIndexPath = _engine.Write(searchRoot, docs);

        await OnIndexWrittenAsync(root, cancellationToken).ConfigureAwait(false);

        var totalBytes = TotalContentBytes(docs);
        SearchLoggingHelper.LogIndexBuildComplete(_logger, docs.Length, totalBytes, searchRoot);
    }

    /// <inheritdoc/>
    public void WriteHeadExtra(IBufferWriter<byte> writer)
    {
        // Lazy-init the manifest URL when ConfigureAsync didn't run (head-extra-only smoke tests).
        _manifestUrlBytes ??= BuildManifestUrlBytes(OutputSubdirectory, _engine.ManifestFileName);

        if (_manifestUrlBytes.Length > 0)
        {
            HeadExtraWriter.WriteUtf8(writer, "<meta name=\"nustreamdocs:search-index\" content=\""u8);
            HeadExtraWriter.WriteUtf8(writer, _manifestUrlBytes);
            HeadExtraWriter.WriteUtf8(writer, "\">\n"u8);
        }

        WriteSectionPrioritiesMeta(writer, SectionPriorities);
        WriteEngineHeadExtra(writer);
    }

    /// <summary>Snapshots the documents harvested so far; exposed for tests.</summary>
    /// <returns>A fresh array copy of the bag's contents.</returns>
    internal SearchDocument[] DocumentsSnapshot() => [.. _documents];

    /// <summary>Override hook invoked after the engine writes its index; engines do post-write work here (CLI invocation, compression sidecars, etc.).</summary>
    /// <param name="siteRoot">Absolute path to the rendered site directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when post-processing has finished.</returns>
    protected virtual ValueTask OnIndexWrittenAsync(DirectoryPath siteRoot, CancellationToken cancellationToken)
    {
        _ = siteRoot;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <summary>Override hook for engine-specific head-extras (script / link tags) emitted after the universal meta tags.</summary>
    /// <param name="writer">Sink for the rendered tags.</param>
    protected virtual void WriteEngineHeadExtra(IBufferWriter<byte> writer) => _ = writer;

    /// <summary>Emits the <c>nustreamdocs:search-section-priorities</c> meta tag when the option is non-empty.</summary>
    /// <param name="writer">Sink for the rendered tag.</param>
    /// <param name="priorities">UTF-8 priority string; empty no-ops.</param>
    private static void WriteSectionPrioritiesMeta(IBufferWriter<byte> writer, byte[] priorities)
    {
        if (priorities is not { Length: > 0 })
        {
            return;
        }

        HeadExtraWriter.WriteUtf8(writer, "<meta name=\"nustreamdocs:search-section-priorities\" content=\""u8);
        HeadExtraWriter.WriteUtf8(writer, priorities);
        HeadExtraWriter.WriteUtf8(writer, "\">\n"u8);
    }

    /// <summary>Filters out documents shorter than <paramref name="minTokenLength"/> and sorts the rest deterministically.</summary>
    /// <param name="bag">Harvested documents.</param>
    /// <param name="minTokenLength">Minimum text length to keep.</param>
    /// <returns>An ordinal-sorted array of kept documents.</returns>
    private static SearchDocument[] FilterAndSort(ConcurrentBag<SearchDocument> bag, int minTokenLength)
    {
        var min = Math.Max(0, minTokenLength);
        List<SearchDocument> docs = new(bag.Count);
        foreach (var doc in bag)
        {
            if (doc.Text.Length >= min)
            {
                docs.Add(doc);
            }
        }

        docs.Sort(static (a, b) => a.RelativeUrl.AsSpan().SequenceCompareTo(b.RelativeUrl.AsSpan()));
        return [.. docs];
    }

    /// <summary>Translates a source-relative markdown path to its rendered page URL.</summary>
    /// <param name="markdownPath">Source-relative path (e.g. <c>guide/intro.md</c>).</param>
    /// <param name="useDirectoryUrls">True when the site emits directory-style URLs.</param>
    /// <returns>Root-relative rendered URL bytes.</returns>
    private static byte[] ToHtmlUrl(in FilePath markdownPath, bool useDirectoryUrls)
    {
        if (markdownPath.IsEmpty)
        {
            return [(byte)'/'];
        }

        var path = useDirectoryUrls ? markdownPath : markdownPath.WithExtension(".html");
        return ServedUrlBytes.FromPath(path, useDirectoryUrls, leadingSlash: true);
    }

    /// <summary>Builds the site-relative manifest URL bytes; empty when the engine ships no fetchable manifest.</summary>
    /// <param name="outputSubdirectory">Site-relative search directory.</param>
    /// <param name="manifestFileName">Engine-supplied manifest filename component (already includes the leading <c>/</c>); empty when none.</param>
    /// <returns>UTF-8 manifest URL bytes, or an empty array.</returns>
    private static byte[] BuildManifestUrlBytes(in PathSegment outputSubdirectory, ReadOnlySpan<byte> manifestFileName)
    {
        if (manifestFileName.IsEmpty)
        {
            return [];
        }

        var subdir = outputSubdirectory.Value.AsSpan();
        var size = 1 + Encoding.UTF8.GetByteCount(subdir) + manifestFileName.Length;
        var result = new byte[size];
        result[0] = (byte)'/';
        var written = 1 + Encoding.UTF8.GetBytes(subdir, result.AsSpan(1));
        manifestFileName.CopyTo(result.AsSpan(written));
        return result;
    }

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
