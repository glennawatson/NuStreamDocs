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

/// <summary>
/// Template-method base for engine-specific search plugins. Drives the page
/// scan, finalize, and head-extra emission stages; defers the actual on-disk
/// index write — and any post-processing such as compression sidecars or CLI
/// invocation — to the engine plugin supplied at construction.
/// </summary>
/// <remarks>
/// <para>
/// Engine plugins (<c>PagefindSearchPlugin</c>, <c>LunrSearchPlugin</c>) hold
/// their own option records and override the protected knob properties
/// (<see cref="OutputSubdirectory"/>, <see cref="MinTokenLength"/>, …). The
/// base library has no shared user-facing options type because every engine
/// has a different set of knobs.
/// </para>
/// <para>
/// Compression of the manifest, glue-asset emission, head-script tag injection
/// and any post-write tooling (e.g. running the Pagefind CLI to produce binary
/// shards) are engine concerns — implement them by overriding
/// <see cref="OnIndexWrittenAsync"/> and <see cref="WriteEngineHeadExtra"/>.
/// </para>
/// </remarks>
public abstract class SearchPluginBase : IBuildConfigurePlugin, IPageScanPlugin, IBuildFinalizePlugin, IHeadExtraProvider
{
    /// <summary>Engine implementation that owns the on-disk format.</summary>
    private readonly ISearchEngine _engine;

    /// <summary>Per-page documents collected during the parallel render stage.</summary>
    private readonly ConcurrentBag<SearchDocument> _documents = [];

    /// <summary>Logger for diagnostics.</summary>
    private readonly ILogger _logger;

    /// <summary>Output root captured during configure.</summary>
    private DirectoryPath _outputRoot;

    /// <summary>Whether the site emits directory-style URLs.</summary>
    private bool _useDirectoryUrls;

    /// <summary>
    /// Pre-encoded site-relative manifest URL bytes (e.g. <c>/search/search_index.json</c>);
    /// computed once when the engine emits a manifest, and reused in <see cref="WriteHeadExtra"/>.
    /// Empty when the engine has no manifest URL to advertise (real Pagefind ships a script
    /// loader instead).
    /// </summary>
    private byte[]? _manifestUrlBytes;

    /// <summary>Initializes a new instance of the <see cref="SearchPluginBase"/> class.</summary>
    /// <param name="engine">Engine implementation that owns the on-disk format.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    protected SearchPluginBase(ISearchEngine engine, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(logger);
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

    /// <summary>Gets the minimum-text-length filter — documents whose extracted body is shorter are dropped before write.</summary>
    protected abstract int MinTokenLength { get; }

    /// <summary>Gets the UTF-8 frontmatter keys whose values are folded into each page's searchable text. Empty for body-only indexing.</summary>
    protected abstract byte[][] SearchableFrontmatterKeys { get; }

    /// <summary>
    /// Gets the UTF-8 <c>prefix:weight</c> pairs surfaced via the
    /// <c>nustreamdocs:search-section-priorities</c> meta tag for theme-JS-driven re-ranking.
    /// Empty disables the meta tag.
    /// </summary>
    protected abstract byte[] SectionPriorities { get; }

    /// <summary>
    /// Gets the path to the most recent index manifest written by <see cref="ISearchEngine.Write"/>;
    /// <see cref="FilePath.IsEmpty"/> when the engine doesn't write a manifest. Exposed so engine
    /// subclasses can build compression sidecars in their <see cref="OnIndexWrittenAsync"/> override.
    /// </summary>
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
        ArgumentNullException.ThrowIfNull(writer);

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

    /// <summary>
    /// Override hook called after the engine's <see cref="ISearchEngine.Write"/> has produced (or
    /// chosen not to produce) a manifest. Engines do all post-write work here — Pagefind invokes
    /// its native CLI to build the WASM runtime + binary shards; Lunr emits <c>.gz</c> / <c>.br</c>
    /// sidecars off <see cref="PrimaryIndexPath"/> for CDN <c>Content-Encoding</c> negotiation.
    /// </summary>
    /// <param name="siteRoot">Absolute path to the rendered site directory (the output root, not the per-engine search subdir).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the post-processing has finished.</returns>
    protected virtual ValueTask OnIndexWrittenAsync(DirectoryPath siteRoot, CancellationToken cancellationToken)
    {
        _ = siteRoot;
        _ = cancellationToken;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Override hook for engine-specific head-extras (script / link tags). Called from
    /// <see cref="WriteHeadExtra"/> after the universal discovery meta tags. Pagefind emits the
    /// <c>&lt;script type="module"&gt;</c> for its WASM loader plus the bind glue here; Lunr
    /// emits its <c>lunr.min.js</c> + bind glue.
    /// </summary>
    /// <param name="writer">Sink for the rendered tags.</param>
    protected virtual void WriteEngineHeadExtra(IBufferWriter<byte> writer)
    {
        _ = writer;
    }

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

    /// <summary>Filters out documents shorter than <paramref name="minTokenLength"/> + sorts deterministically.</summary>
    /// <param name="bag">Concurrent bag of harvested docs.</param>
    /// <param name="minTokenLength">Minimum text length to keep.</param>
    /// <returns>A right-sized, ordinal-sorted array.</returns>
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
    private static byte[] ToHtmlUrl(FilePath markdownPath, bool useDirectoryUrls)
    {
        if (markdownPath.IsEmpty)
        {
            return [(byte)'/'];
        }

        var path = useDirectoryUrls ? markdownPath : markdownPath.WithExtension(".html");
        return ServedUrlBytes.FromPath(path, useDirectoryUrls, leadingSlash: true);
    }

    /// <summary>
    /// Builds the site-relative manifest URL bytes (e.g. <c>/search/search_index.json</c>) for the
    /// chosen engine; returns an empty array when the engine doesn't ship a manifest the theme
    /// should fetch.
    /// </summary>
    /// <param name="outputSubdirectory">Site-relative search directory.</param>
    /// <param name="manifestFileName">Engine-supplied manifest filename component (already includes the leading <c>/</c>); empty when the engine has no fetchable manifest.</param>
    /// <returns>UTF-8 manifest URL bytes, or an empty array.</returns>
    private static byte[] BuildManifestUrlBytes(PathSegment outputSubdirectory, ReadOnlySpan<byte> manifestFileName)
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
