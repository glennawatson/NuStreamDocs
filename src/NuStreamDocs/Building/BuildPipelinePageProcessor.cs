// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using NuStreamDocs.Caching;
using NuStreamDocs.Common;
using NuStreamDocs.Logging;
using NuStreamDocs.Markdown.Common;
using NuStreamDocs.Plugins;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Building;

/// <summary>
/// Static helper for processing individual pages through the build pipeline.
/// </summary>
internal static class BuildPipelinePageProcessor
{
    /// <summary>Initial-capacity multiplier for preprocessor scratch buffers — pages typically grow only modestly through rewrites.</summary>
    private const int PreprocessorScratchMultiplier = 2;

    /// <summary>
    /// Renders one page or short-circuits via the manifest. When the build needs cross-page
    /// resolution, transfers the rendered rental to the buffered queue instead of writing
    /// immediately.
    /// </summary>
    /// <param name="item">Page work item.</param>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="useDirectoryUrls">Selects the output-path shape (flat vs <c>foo/index.html</c>).</param>
    /// <param name="dispatch">Bundle of per-page shared state (phases, previous manifest, timing, buffered queue).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fresh manifest entry, a flag indicating cache hit, and a flag indicating whether the rental was transferred to the buffered queue.</returns>
    public static async ValueTask<(ManifestEntry Entry, bool CacheHit, bool DidBuffer)> ProcessOnePageAsync(
        PageWorkItem item,
        DirectoryPath outputRoot,
        bool useDirectoryUrls,
        PerPageDispatch dispatch,
        CancellationToken cancellationToken)
    {
        // Synthetic pages skip the file-read entirely — bytes already live in process memory,
        // there's nothing to pool or release. Disk pages take the RandomAccess + ArrayPool
        // path: File.ReadAllBytesAsync allocates a fresh byte[] per page (158 MB on a 13.8K-page
        // corpus) and FileStream's BufferedFileStreamStrategy chain layers on more allocations.
        if (item.InMemorySource is { } memorySource)
        {
            return await ProcessSourceAsync(
                item,
                memorySource.AsMemory(),
                outputRoot,
                useDirectoryUrls,
                dispatch,
                cancellationToken).ConfigureAwait(false);
        }

        using var sourceHandle = File.OpenHandle(
            item.AbsolutePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var sourceLength = (int)RandomAccess.GetLength(sourceHandle);
        var sourceBuffer = ArrayPool<byte>.Shared.Rent(sourceLength);
        try
        {
            await RandomAccess.ReadAsync(sourceHandle, sourceBuffer.AsMemory(0, sourceLength), 0, cancellationToken)
                .ConfigureAwait(false);
            return await ProcessSourceAsync(
                item,
                sourceBuffer.AsMemory(0, sourceLength),
                outputRoot,
                useDirectoryUrls,
                dispatch,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sourceBuffer);
        }
    }

    /// <summary>Runs the cross-page resolve barrier sequentially, then drains buffered pages through PostResolve and writes them to disk.</summary>
    /// <param name="bufferedPages">Pages held back from immediate write because the build registered cross-page-state plugins.</param>
    /// <param name="postResolves">Sorted post-resolve participants.</param>
    /// <param name="fresh">Manifest queue receiving entries for buffered pages once written.</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every buffered page has been written and its rental disposed.</returns>
    public static async Task DrainBufferedPagesAsync(
        ConcurrentQueue<BufferedPage> bufferedPages,
        IPagePostResolvePlugin[] postResolves,
        ConcurrentQueue<ManifestEntry> fresh,
        BuildPhaseShell shell,
        CancellationToken cancellationToken)
    {
        var pluginTiming = shell.PluginTiming;
        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = shell.Options.Parallelism
        };

        await Parallel.ForEachAsync(bufferedPages, parallelOptions, (page, _) =>
        {
            List<PageBuilderRental> scratch = [];
            var owned = page.Rental;
            try
            {
                var finalRental =
                    ApplyPostResolves(page.Rental, scratch, postResolves, page.RelativePath, pluginTiming);
                if (!finalRental.Equals(page.Rental))
                {
                    owned = finalRental;
                }

                EnsureDirectory(page.OutputPath);
                File.WriteAllBytes(page.OutputPath, finalRental.Writer.WrittenSpan);
                fresh.Enqueue(new(page.RelativePath, page.Hash, finalRental.Writer.WrittenCount));
            }
            finally
            {
                for (var i = 0; i < scratch.Count; i++)
                {
                    scratch[i].Dispose();
                }

                owned.Dispose();
            }

            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);
    }

    /// <summary>Computes the on-disk output path for a relative source path.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="relativePath">Source-relative path.</param>
    /// <param name="useDirectoryUrls">When true, emits non-index pages as <c>foo/index.html</c>; when false, emits as <c>foo.html</c>.</param>
    /// <returns>The absolute output path with the <c>.html</c> extension.</returns>
    public static FilePath OutputPathFor(in DirectoryPath outputRoot, in FilePath relativePath, bool useDirectoryUrls)
    {
        // 404.md always emits as /404.html at the site root, regardless of the
        // directory-URL toggle — most static hosts (GitHub Pages, Netlify, S3
        // static-website mode) only honour /404.html for not-found responses.
        if (string.Equals(relativePath.Value, "404.md", StringComparison.OrdinalIgnoreCase))
        {
            return OutputPathBuilder.ForFlatUrls(outputRoot, relativePath);
        }

        return useDirectoryUrls
            ? OutputPathBuilder.ForDirectoryUrls(outputRoot, relativePath)
            : OutputPathBuilder.ForFlatUrls(outputRoot, relativePath);
    }

    /// <summary>
    /// Streams disk-loaded markdown pages first, then drains the synthetic-page sink so
    /// plugin-registered in-memory pages flow through the same render pipeline without
    /// ever landing in the source folder.
    /// </summary>
    /// <param name="inputRoot">Absolute docs root.</param>
    /// <param name="filter">Include/exclude path filter.</param>
    /// <param name="syntheticPages">Sink populated during the discover phase.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Combined async stream of work items.</returns>
    public static async IAsyncEnumerable<PageWorkItem> EnumerateDiskAndSyntheticAsync(
        DirectoryPath inputRoot,
        PathFilter filter,
        SyntheticPageSink syntheticPages,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in PageDiscovery.EnumerateAsync(inputRoot.Value, filter, cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return item;
        }

        if (syntheticPages.Count is 0 && syntheticPages.StreamCount is 0)
        {
            yield break;
        }

        // Drain eager pages first, then any registered streams. The sink yields one item at a
        // time so a high-fanout producer (e.g. the C# API generator) keeps peak memory low —
        // each page renders and writes before the next one is pulled.
        await foreach (var page in syntheticPages.DrainAsync(cancellationToken).ConfigureAwait(false))
        {
            var flags = FrontmatterFlagReader.ReadFlags(page.MarkdownBytes);
            yield return new(default, page.RelativePath, flags) { InMemorySource = page.MarkdownBytes };
        }
    }

    /// <summary>
    /// Renders one page given its already-materialized UTF-8 source. Shared body of <see cref="ProcessOnePageAsync"/>'s
    /// disk-loaded and synthetic-page branches — they differ only in how they obtain the source memory.
    /// </summary>
    /// <param name="item">Page work item.</param>
    /// <param name="raw">UTF-8 source bytes (BOM still present; stripped below).</param>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="useDirectoryUrls">Selects the output-path shape (flat vs <c>foo/index.html</c>).</param>
    /// <param name="dispatch">Bundle of per-page shared state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fresh manifest entry, a flag indicating cache hit, and a flag indicating whether the rental was transferred to the buffered queue.</returns>
    private static async ValueTask<(ManifestEntry Entry, bool CacheHit, bool DidBuffer)> ProcessSourceAsync(
        PageWorkItem item,
        ReadOnlyMemory<byte> raw,
        DirectoryPath outputRoot,
        bool useDirectoryUrls,
        PerPageDispatch dispatch,
        CancellationToken cancellationToken)
    {
        var hash = ContentHasher.Hash(raw.Span);
        var source = raw[Utf8Bom.LengthOf(raw.Span)..];
        var outputPath = OutputPathFor(outputRoot, item.RelativePath, useDirectoryUrls);
        var phases = dispatch.Phases;
        var pluginTiming = dispatch.PluginTiming;

        if (dispatch.Previous.TryGet(item.RelativePath, out var stale) &&
            stale.ContentHash.AsSpan().SequenceEqual(hash) &&
            File.Exists(outputPath))
        {
            // Hot incremental path: source matches previous build and output is on disk.
            // Re-fire IPageScanPlugin.Scan against the cached output so plugins like
            // SearchPluginBase / LinkValidator still observe every page — without this,
            // cached pages would be invisible to scan-phase plugins and incremental
            // rebuilds would emit indexes / validators that only know about pages
            // re-rendered this build.
            if (phases.Scans.Length > 0)
            {
                var cachedHtml = await File.ReadAllBytesAsync(outputPath, cancellationToken).ConfigureAwait(false);
                FireScans(phases.Scans, item.RelativePath, source.Span, cachedHtml, pluginTiming);
            }

            return (stale, true, false);
        }

        var rental = PageBuilderPool.Rent(source.Length * 2);
        List<PageBuilderRental> scratchRentals = [];
        PageBuilderRental? owned = rental;
        try
        {
            var processedSource =
                ApplyPreRenders(source, phases.PreRenders, scratchRentals, item.RelativePath, pluginTiming);
            MarkdownRenderer.Render(processedSource.Span, rental.Writer);

            var finalRental = ApplyPostRenders(
                rental,
                scratchRentals,
                source.Span,
                phases.PostRenders,
                item.RelativePath,
                pluginTiming);
            if (!finalRental.Equals(rental))
            {
                // ApplyPostRenders moved `rental` into scratch (it's now stale). Track final instead.
                owned = finalRental;
            }

            FireScans(phases.Scans, item.RelativePath, source.Span, finalRental.Writer.WrittenSpan, pluginTiming);

            var pageNeedsBarrier = phases.NeedsCrossPageBarrier
                                   && PageHasCrossPageMarker(
                                       finalRental.Writer.WrittenSpan,
                                       dispatch.CrossPageMarkerNeedles);
            if (pageNeedsBarrier)
            {
                // Transfer rental ownership to the buffered queue. The drain phase
                // disposes the rental after PostResolve + Write.
                dispatch.Buffered.Enqueue(new(item.RelativePath, outputPath, finalRental, hash));
                owned = null;
                return (default, false, true);
            }

            EnsureDirectory(outputPath);

            // Sync write skips the BufferedFileStreamStrategy + ThreadPoolValueTaskSource
            // alloc chain. Page bytes are already in memory; nothing to overlap with.
            File.WriteAllBytes(outputPath, finalRental.Writer.WrittenSpan);
            return (new(item.RelativePath, hash, finalRental.Writer.WrittenCount), false, false);
        }
        finally
        {
            for (var i = 0; i < scratchRentals.Count; i++)
            {
                scratchRentals[i].Dispose();
            }

            if (owned is { } o)
            {
                o.Dispose();
            }
        }
    }

    /// <summary>Threads <paramref name="source"/> through every <see cref="IPagePreRenderPlugin"/> sorted by priority.</summary>
    /// <param name="source">UTF-8 markdown bytes as read from disk.</param>
    /// <param name="plugins">Sorted pre-render participants.</param>
    /// <param name="scratch">Output rental list — the caller disposes every entry.</param>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="pluginTiming">Per-plugin time accumulator.</param>
    /// <returns>The rewritten bytes, or <paramref name="source"/> unchanged when no participant rewrites.</returns>
    private static ReadOnlyMemory<byte> ApplyPreRenders(
        in ReadOnlyMemory<byte> source,
        IPagePreRenderPlugin[] plugins,
        List<PageBuilderRental> scratch,
        in FilePath relativePath,
        PluginTimingTable pluginTiming)
    {
        // CommonMark reference-style links are a core spec feature, not a plugin. Resolve them
        // before any preprocessor sees the source so plugins like Tables (which inline-renders
        // each cell at emission time) get already-inlined `[text](url)` links and emit proper
        // anchors rather than the literal `[text][label]` text.
        var current = source;
        if (LinkReferenceRewriter.MayContainReferences(current.Span))
        {
            current = LinkReferenceRewriter.Rewrite(current.Span);
        }

        if (plugins.Length is 0)
        {
            return current;
        }

        PageBuilderRental? front = null;
        PageBuilderRental? back = null;
        for (var i = 0; i < plugins.Length; i++)
        {
            var plugin = plugins[i];
            if (!plugin.NeedsRewrite(current.Span))
            {
                continue;
            }

            // First-rewrite path: lazy-rent front, write, set current. Common case for single-plugin
            // pipelines (Macros / Snippets / Bibliography) where the plugin's NeedsRewrite filters
            // out 99%+ of pages.
            if (front is null)
            {
                var capacity = source.Length * PreprocessorScratchMultiplier;
                front = PageBuilderPool.Rent(capacity);
                scratch.Add(front.Value);
                var fr = front.Value;
                fr.Writer.ResetWrittenCount();
                PagePreRenderContext ctx0 = new(relativePath, current.Span, fr.Writer);
                using (pluginTiming.Measure(plugin.Name))
                {
                    plugin.PreRender(in ctx0);
                }

                current = fr.Writer.WrittenMemory;
                continue;
            }

            // Subsequent-rewrite path: lazy-rent back once, ping-pong each iteration.
            if (back is null)
            {
                var capacity = source.Length * PreprocessorScratchMultiplier;
                back = PageBuilderPool.Rent(capacity);
                scratch.Add(back.Value);
            }

            var target = back.Value;
            target.Writer.ResetWrittenCount();
            PagePreRenderContext ctx = new(relativePath, current.Span, target.Writer);
            using (pluginTiming.Measure(plugin.Name))
            {
                plugin.PreRender(in ctx);
            }

            current = target.Writer.WrittenMemory;
            (front, back) = (back, front);
        }

        return current;
    }

    /// <summary>Runs every <see cref="IPagePostRenderPlugin"/> in priority order, ping-ponging buffers when a participant rewrites.</summary>
    /// <param name="input">Rental holding the rendered HTML.</param>
    /// <param name="scratch">Output rental list — the caller disposes every entry. <paramref name="input"/> is added here when a swap moves it out of the active position.</param>
    /// <param name="source">Original UTF-8 markdown bytes (passed to plugins via context).</param>
    /// <param name="plugins">Sorted post-render participants.</param>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="pluginTiming">Per-plugin time accumulator.</param>
    /// <returns>The rental whose writer holds the final post-render HTML — may differ from <paramref name="input"/>.</returns>
    private static PageBuilderRental ApplyPostRenders(
        in PageBuilderRental input,
        List<PageBuilderRental> scratch,
        ReadOnlySpan<byte> source,
        IPagePostRenderPlugin[] plugins,
        in FilePath relativePath,
        PluginTimingTable pluginTiming)
    {
        if (plugins.Length is 0)
        {
            return input;
        }

        // Single-pass loop: ask each plugin in order whether it wants to rewrite, and only
        // rent the swap rental on the first rewrite that actually fires. The previous shape
        // had a separate "any rewrites?" pre-check which doubled the NeedsRewrite count on
        // the marker-free hot path (and the WithNav benchmark hit it on every page).
        PageBuilderRental? back = null;
        var front = input;
        for (var i = 0; i < plugins.Length; i++)
        {
            var plugin = plugins[i];
            if (!plugin.NeedsRewrite(front.Writer.WrittenSpan))
            {
                continue;
            }

            if (back is null)
            {
                var capacity = Math.Max(input.Writer.WrittenCount, source.Length) * PreprocessorScratchMultiplier;
                back = PageBuilderPool.Rent(capacity);
            }

            var swap = back.Value;
            swap.Writer.ResetWrittenCount();
            PagePostRenderContext ctx = new(relativePath, source, front.Writer.WrittenSpan, swap.Writer);
            using (pluginTiming.Measure(plugin.Name))
            {
                plugin.PostRender(in ctx);
            }

            back = front;
            front = swap;
        }

        // After the loop, `back` is the rental NOT holding the final bytes — always stale
        // when at least one plugin rewrote. The caller's owned-rental tracking points at
        // `front` for disposal; everything else (the original input or the rented swap,
        // whichever isn't `front`) goes through scratch.
        if (back is { } stale)
        {
            scratch.Add(stale);
        }

        return front;
    }

    /// <summary>Runs every <see cref="IPagePostResolvePlugin"/> in priority order, ping-ponging buffers when a participant rewrites.</summary>
    /// <param name="input">Rental holding the post-render HTML.</param>
    /// <param name="scratch">Output rental list — caller disposes every entry.</param>
    /// <param name="plugins">Sorted post-resolve participants.</param>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="pluginTiming">Per-plugin time accumulator.</param>
    /// <returns>The rental whose writer holds the final post-resolve HTML.</returns>
    private static PageBuilderRental ApplyPostResolves(
        in PageBuilderRental input,
        List<PageBuilderRental> scratch,
        IPagePostResolvePlugin[] plugins,
        in FilePath relativePath,
        PluginTimingTable pluginTiming)
    {
        if (plugins.Length is 0)
        {
            return input;
        }

        var anyRewrites = false;
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i].NeedsRewrite(input.Writer.WrittenSpan))
            {
                anyRewrites = true;
                break;
            }
        }

        if (!anyRewrites)
        {
            return input;
        }

        var capacity = input.Writer.WrittenCount * PreprocessorScratchMultiplier;
        var back = PageBuilderPool.Rent(capacity);

        var front = input;
        for (var i = 0; i < plugins.Length; i++)
        {
            var plugin = plugins[i];
            if (!plugin.NeedsRewrite(front.Writer.WrittenSpan))
            {
                continue;
            }

            back.Writer.ResetWrittenCount();
            PagePostResolveContext ctx = new(relativePath, front.Writer.WrittenSpan, back.Writer);
            using (pluginTiming.Measure(plugin.Name))
            {
                plugin.Rewrite(in ctx);
            }

            (front, back) = (back, front);
        }

        scratch.Add(back);
        return front;
    }

    /// <summary>Fires every <see cref="IPageScanPlugin.Scan"/> in priority order against the post-render HTML.</summary>
    /// <param name="scans">Sorted scan participants.</param>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="source">Original UTF-8 markdown bytes.</param>
    /// <param name="html">Post-render HTML bytes.</param>
    /// <param name="pluginTiming">Per-plugin time accumulator.</param>
    private static void FireScans(
        IPageScanPlugin[] scans,
        in FilePath relativePath,
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> html,
        PluginTimingTable pluginTiming)
    {
        if (scans.Length is 0)
        {
            return;
        }

        PageScanContext scanContext = new(relativePath, source, html);
        for (var i = 0; i < scans.Length; i++)
        {
            var plugin = scans[i];
            using (pluginTiming.Measure(plugin.Name))
            {
                plugin.Scan(in scanContext);
            }
        }
    }

    /// <summary>
    /// True when at least one <paramref name="needles"/> entry is present in
    /// <paramref name="html"/>, or when <paramref name="needles"/> is empty (in which case every
    /// page is buffered).
    /// </summary>
    /// <param name="html">UTF-8 HTML of the rendered page.</param>
    /// <param name="needles">Marker byte sequences registered by cross-page plugins during configure.</param>
    /// <returns>True when the page must be held for the cross-page barrier; false when it can write immediately.</returns>
    private static bool PageHasCrossPageMarker(ReadOnlySpan<byte> html, byte[][] needles)
    {
        if (needles.Length is 0)
        {
            // No registered markers means the cross-page barrier still has to drain
            // (e.g. an IBuildResolvePlugin that walks every page during the barrier
            // expects every page to be available); fall back to buffering everything.
            return true;
        }

        for (var i = 0; i < needles.Length; i++)
        {
            if (html.IndexOf(needles[i]) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Ensures the directory containing <paramref name="outputPath"/> exists.</summary>
    /// <param name="outputPath">Absolute output path.</param>
    private static void EnsureDirectory(string outputPath)
    {
        var dirSpan = Path.GetDirectoryName(outputPath.AsSpan());
        if (dirSpan.IsEmpty)
        {
            return;
        }

        Directory.CreateDirectory(dirSpan.ToString());
    }
}
