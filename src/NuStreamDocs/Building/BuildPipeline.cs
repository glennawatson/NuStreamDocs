// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Caching;
using NuStreamDocs.Common;
using NuStreamDocs.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Building;

/// <summary>
/// Static driver that streams <see cref="PageWorkItem"/>s through the
/// per-page parse, render, plugin and write stages.
/// </summary>
/// <remarks>
/// Pipeline phases run in order: Configure → Discover → (parallel per-page:
/// PreRender → Render → PostRender → Scan → either immediate Write or
/// buffer-for-Resolve) → cross-page Resolve barrier (sequential) → drain
/// buffered pages (PostResolve → Write) → static-asset copy → manifest
/// save → Finalize. Each phase iterates only the participants for that
/// phase, sorted once at build start by <see cref="PluginPriority"/>.
/// </remarks>
public static class BuildPipeline
{
    /// <summary>Default parallel-worker cap; tuned for laptops and CI runners.</summary>
    private const int DefaultParallelism = 8;

    /// <summary>Milliseconds per second for user-facing elapsed-time conversion.</summary>
    private const double MillisecondsPerSecond = 1000d;

    /// <summary>Initial-capacity multiplier for preprocessor scratch buffers — pages typically grow only modestly through rewrites.</summary>
    private const int PreprocessorScratchMultiplier = 2;

    /// <summary>Per-thread cache of the most recently created output directory; pages in the same directory share the slot to skip the syscall.</summary>
    [ThreadStatic]
    private static string? _lastCreatedDirectory;

    /// <summary>Runs the build with no cancellation support and default options.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <returns>The total number of pages processed (rendered + skipped).</returns>
    public static Task<int> RunAsync(DirectoryPath inputRoot, DirectoryPath outputRoot, IPlugin[] plugins) =>
        RunAsync(inputRoot, outputRoot, plugins, BuildPipelineOptions.Default, CancellationToken.None);

    /// <summary>Runs the build with cancellation support and default options.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of pages processed.</returns>
    public static Task<int> RunAsync(
        DirectoryPath inputRoot,
        DirectoryPath outputRoot,
        IPlugin[] plugins,
        in CancellationToken cancellationToken) =>
        RunAsync(inputRoot, outputRoot, plugins, BuildPipelineOptions.Default, cancellationToken);

    /// <summary>Canonical build entry point: runs the pipeline with explicit <paramref name="options"/>.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="options">Pipeline options (filter, logger, URL shape, draft toggle).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of pages processed.</returns>
    public static async Task<int> RunAsync(
        DirectoryPath inputRoot,
        DirectoryPath outputRoot,
        IPlugin[] plugins,
        BuildPipelineOptions options,
        CancellationToken cancellationToken)
    {
        if (inputRoot.IsEmpty)
        {
            throw new ArgumentException("Input root must be non-empty.", nameof(inputRoot));
        }

        if (outputRoot.IsEmpty)
        {
            throw new ArgumentException("Output root must be non-empty.", nameof(outputRoot));
        }

        ArgumentNullException.ThrowIfNull(plugins);
        var filter = options.Filter ?? PathFilter.Empty;
        var useDirectoryUrls = options.UseDirectoryUrls;
        var includeDrafts = options.IncludeDrafts;

        var log = options.Logger ?? NullLogger.Instance;
        BuildPipelineLoggingHelper.LogBuildStart(log, inputRoot.Value, outputRoot.Value, plugins.Length);
        var stopwatch = Stopwatch.StartNew();
        var pluginTiming = new PluginTimingTable();
        var buildFingerprint = BuildFingerprint.Create(plugins, options);

        Directory.CreateDirectory(outputRoot);
        var previous = await BuildManifest.LoadAsync(outputRoot, buildFingerprint, cancellationToken, log).ConfigureAwait(false);

        // ConcurrentQueue's segmented linked-list outperforms ConcurrentBag for the
        // append-only-from-many-threads / drain-once pattern, and ToArray() is a
        // single right-sized allocation rather than the [.. bag] enumerator copy.
        var fresh = new ConcurrentQueue<ManifestEntry>();
        var bufferedPages = new ConcurrentQueue<BufferedPage>();

        // Partition into per-phase sorted arrays (one allocation per phase, once per build).
        var phases = PluginPhases.Partition(plugins);
        var crossPageMarkers = new CrossPageMarkerRegistry();
        var shell = new BuildPhaseShell(inputRoot, outputRoot, options, pluginTiming, log);

        BuildPipelineLoggingHelper.LogConfigureStart(log, phases.Configures.Length);
        await FireConfigureAsync(phases.Configures, plugins, shell, crossPageMarkers, cancellationToken).ConfigureAwait(false);
        await FireDiscoverAsync(phases.Discovers, plugins, shell, cancellationToken).ConfigureAwait(false);

        var processed = 0;
        var cacheHits = 0;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = DefaultParallelism,
        };

        var perPage = new PerPageDispatch(phases, previous, pluginTiming, bufferedPages);

        BuildPipelineLoggingHelper.LogRenderStart(log, parallelOptions.MaxDegreeOfParallelism);
        var renderStarted = stopwatch.ElapsedMilliseconds;
        await Parallel.ForEachAsync(
            PageDiscovery.EnumerateAsync(inputRoot, filter, cancellationToken),
            parallelOptions,
            async (item, ct) =>
            {
                if (!includeDrafts && (item.Flags & PageFlags.Draft) != 0)
                {
                    return;
                }

                var (entry, hit, didBuffer) = await ProcessOnePageAsync(item, outputRoot, useDirectoryUrls, perPage, ct).ConfigureAwait(false);
                if (!didBuffer)
                {
                    fresh.Enqueue(entry);
                }

                Interlocked.Increment(ref processed);
                if (hit)
                {
                    Interlocked.Increment(ref cacheHits);
                }

                BuildPipelineLoggingHelper.LogPageProcessed(log, item.RelativePath, hit);
            }).ConfigureAwait(false);

        BuildPipelineLoggingHelper.LogRenderComplete(log, processed, (stopwatch.ElapsedMilliseconds - renderStarted) / MillisecondsPerSecond);

        if (phases.NeedsCrossPageBarrier)
        {
            await FireResolveAsync(phases.Resolves, plugins, shell, cancellationToken).ConfigureAwait(false);
            await DrainBufferedPagesAsync(bufferedPages, phases.PostResolves, fresh, shell, cancellationToken).ConfigureAwait(false);
        }

        // Copy author-supplied static content from docs/ to site/ — images, fonts, vendor JS,
        // anything the page templates or theme options reference by docs-relative path. Runs
        // before finalize so plugins like sitemap/search/privacy see the assets in place.
        var assetsCopied = DocsAssetCopier.Copy(inputRoot, outputRoot, filter);
        BuildPipelineLoggingHelper.LogAssetsCopied(log, assetsCopied);

        previous.Replace(fresh);
        await previous.SaveAsync(outputRoot, cancellationToken, log).ConfigureAwait(false);

        BuildPipelineLoggingHelper.LogFinalizeStart(log, phases.Finalizes.Length);
        await FireFinalizeAsync(phases.Finalizes, plugins, shell, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        pluginTiming.Emit(log);
        BuildPipelineLoggingHelper.LogBuildComplete(log, processed, cacheHits, stopwatch.ElapsedMilliseconds / MillisecondsPerSecond);
        return processed;
    }

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
    private static async ValueTask<(ManifestEntry Entry, bool CacheHit, bool DidBuffer)> ProcessOnePageAsync(
        PageWorkItem item,
        DirectoryPath outputRoot,
        bool useDirectoryUrls,
        PerPageDispatch dispatch,
        CancellationToken cancellationToken)
    {
        // Read into a pooled buffer instead of File.ReadAllBytesAsync — the
        // latter allocates a fresh byte[] per page (158 MB on a 13.8K-page corpus).
        // RandomAccess avoids the FileStream + BufferedFileStreamStrategy buffer
        // chain entirely; the rented array is returned in the outer finally.
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
            await RandomAccess.ReadAsync(sourceHandle, sourceBuffer.AsMemory(0, sourceLength), 0, cancellationToken).ConfigureAwait(false);
            var source = sourceBuffer.AsMemory(0, sourceLength);
            var hash = ContentHasher.Hash(source.Span);
            var outputPath = OutputPathFor(outputRoot, item.RelativePath, useDirectoryUrls);
            var phases = dispatch.Phases;
            var pluginTiming = dispatch.PluginTiming;

            if (dispatch.Previous.TryGet(item.RelativePath, out var stale) &&
                stale.ContentHash.AsSpan().SequenceEqual(hash) &&
                File.Exists(outputPath))
            {
                // Hot incremental path: source matches previous build and output is on disk.
                return (stale, true, false);
            }

            var rental = PageBuilderPool.Rent(source.Length * 2);
            var scratchRentals = new List<PageBuilderRental>();
            PageBuilderRental? owned = rental;
            try
            {
                var processedSource = ApplyPreRenders(source, phases.PreRenders, scratchRentals, item.RelativePath, pluginTiming);
                MarkdownRenderer.Render(processedSource.Span, rental.Writer);

                var finalRental = ApplyPostRenders(rental, scratchRentals, source.Span, phases.PostRenders, item.RelativePath, pluginTiming);
                if (!finalRental.Equals(rental))
                {
                    // ApplyPostRenders moved `rental` into scratch (it's now stale). Track final instead.
                    owned = finalRental;
                }

                if (phases.Scans.Length > 0)
                {
                    var scanContext = new PageScanContext(item.RelativePath, source.Span, finalRental.Writer.WrittenSpan);
                    for (var i = 0; i < phases.Scans.Length; i++)
                    {
                        var plugin = phases.Scans[i];
                        using (pluginTiming.Measure(plugin.Name))
                        {
                            plugin.Scan(in scanContext);
                        }
                    }
                }

                if (phases.NeedsCrossPageBarrier)
                {
                    // Transfer rental ownership to the buffered queue. The drain phase
                    // disposes the rental after PostResolve + Write.
                    dispatch.Buffered.Enqueue(new BufferedPage(item.RelativePath, outputPath, finalRental, hash));
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
        finally
        {
            ArrayPool<byte>.Shared.Return(sourceBuffer);
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
        ReadOnlyMemory<byte> source,
        IPagePreRenderPlugin[] plugins,
        List<PageBuilderRental> scratch,
        FilePath relativePath,
        PluginTimingTable pluginTiming)
    {
        if (plugins.Length is 0)
        {
            return source;
        }

        var capacity = source.Length * PreprocessorScratchMultiplier;
        var front = PageBuilderPool.Rent(capacity);
        scratch.Add(front);

        PageBuilderRental? back = null;
        if (plugins.Length > 1)
        {
            var b = PageBuilderPool.Rent(capacity);
            scratch.Add(b);
            back = b;
        }

        var current = source;
        for (var i = 0; i < plugins.Length; i++)
        {
            var plugin = plugins[i];
            if (!plugin.NeedsRewrite(current.Span))
            {
                continue;
            }

            front.Writer.ResetWrittenCount();
            var ctx = new PagePreRenderContext(relativePath, current.Span, front.Writer);
            using (pluginTiming.Measure(plugin.Name))
            {
                plugin.PreRender(in ctx);
            }

            current = front.Writer.WrittenMemory;

            if (back is null)
            {
                continue;
            }

            (front, back) = (back.Value, front);
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
        PageBuilderRental input,
        List<PageBuilderRental> scratch,
        ReadOnlySpan<byte> source,
        IPagePostRenderPlugin[] plugins,
        FilePath relativePath,
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

        var capacity = Math.Max(input.Writer.WrittenCount, source.Length) * PreprocessorScratchMultiplier;
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
            var ctx = new PagePostRenderContext(relativePath, source, front.Writer.WrittenSpan, back.Writer);
            using (pluginTiming.Measure(plugin.Name))
            {
                plugin.PostRender(in ctx);
            }

            (front, back) = (back, front);
        }

        // `back` is now the stale rental — push to scratch for disposal.
        scratch.Add(back);
        return front;
    }

    /// <summary>Runs the cross-page resolve barrier sequentially, then drains buffered pages through PostResolve and writes them to disk.</summary>
    /// <param name="bufferedPages">Pages held back from immediate write because the build registered cross-page-state plugins.</param>
    /// <param name="postResolves">Sorted post-resolve participants.</param>
    /// <param name="fresh">Manifest queue receiving entries for buffered pages once written.</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every buffered page has been written and its rental disposed.</returns>
    private static async Task DrainBufferedPagesAsync(
        ConcurrentQueue<BufferedPage> bufferedPages,
        IPagePostResolvePlugin[] postResolves,
        ConcurrentQueue<ManifestEntry> fresh,
        BuildPhaseShell shell,
        CancellationToken cancellationToken)
    {
        var pluginTiming = shell.PluginTiming;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = DefaultParallelism,
        };

        await Parallel.ForEachAsync(bufferedPages, parallelOptions, (page, _) =>
        {
            var scratch = new List<PageBuilderRental>();
            var owned = page.Rental;
            try
            {
                var finalRental = ApplyPostResolves(page.Rental, scratch, postResolves, page.RelativePath, pluginTiming);
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

    /// <summary>Runs every <see cref="IPagePostResolvePlugin"/> in priority order, ping-ponging buffers when a participant rewrites.</summary>
    /// <param name="input">Rental holding the post-render HTML.</param>
    /// <param name="scratch">Output rental list — caller disposes every entry.</param>
    /// <param name="plugins">Sorted post-resolve participants.</param>
    /// <param name="relativePath">Page path relative to the input root.</param>
    /// <param name="pluginTiming">Per-plugin time accumulator.</param>
    /// <returns>The rental whose writer holds the final post-resolve HTML.</returns>
    private static PageBuilderRental ApplyPostResolves(
        PageBuilderRental input,
        List<PageBuilderRental> scratch,
        IPagePostResolvePlugin[] plugins,
        FilePath relativePath,
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
            var ctx = new PagePostResolveContext(relativePath, front.Writer.WrittenSpan, back.Writer);
            using (pluginTiming.Measure(plugin.Name))
            {
                plugin.Rewrite(in ctx);
            }

            (front, back) = (back, front);
        }

        scratch.Add(back);
        return front;
    }

    /// <summary>Ensures the directory containing <paramref name="outputPath"/> exists, with a per-thread last-seen cache so repeat directories cost nothing.</summary>
    /// <param name="outputPath">Absolute output path.</param>
    private static void EnsureDirectory(string outputPath)
    {
        var dirSpan = Path.GetDirectoryName(outputPath.AsSpan());
        if (dirSpan.IsEmpty)
        {
            return;
        }

        var cached = _lastCreatedDirectory;
        if (cached is not null && dirSpan.SequenceEqual(cached.AsSpan()))
        {
            return;
        }

        var dir = dirSpan.ToString();
        Directory.CreateDirectory(dir);
        _lastCreatedDirectory = dir;
    }

    /// <summary>Computes the on-disk output path for a relative source path.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="relativePath">Source-relative path.</param>
    /// <param name="useDirectoryUrls">When true, emits non-index pages as <c>foo/index.html</c>; when false, emits as <c>foo.html</c>.</param>
    /// <returns>The absolute output path with the <c>.html</c> extension.</returns>
    private static FilePath OutputPathFor(DirectoryPath outputRoot, FilePath relativePath, bool useDirectoryUrls)
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

    /// <summary>Fires <see cref="IBuildConfigurePlugin.ConfigureAsync"/> on every plugin sorted by priority.</summary>
    /// <param name="configures">Sorted configure participants.</param>
    /// <param name="allPlugins">Every registered plugin (passed to participants for sibling discovery).</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="crossPageMarkers">Cross-page marker registry plugins seed during configure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every participant's configure hook has settled.</returns>
    private static async Task FireConfigureAsync(
        IBuildConfigurePlugin[] configures,
        IPlugin[] allPlugins,
        BuildPhaseShell shell,
        CrossPageMarkerRegistry crossPageMarkers,
        CancellationToken cancellationToken)
    {
        if (configures.Length is 0)
        {
            return;
        }

        var options = shell.Options;
        var context = new BuildConfigureContext(shell.InputRoot, shell.OutputRoot, allPlugins, crossPageMarkers)
        {
            UseDirectoryUrls = options.UseDirectoryUrls,
            SiteName = options.SiteName ?? [],
            SiteUrl = options.SiteUrl ?? [],
            SiteAuthor = options.SiteAuthor ?? [],
        };
        var log = shell.Log;
        var pluginTiming = shell.PluginTiming;
        for (var i = 0; i < configures.Length; i++)
        {
            var plugin = configures[i];
            BuildPipelineLoggingHelper.LogPluginConfigure(log, plugin.Name);
            using (pluginTiming.Measure(plugin.Name))
            {
                await plugin.ConfigureAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Fires <see cref="IBuildDiscoverPlugin.DiscoverAsync"/> on every plugin sorted by priority.</summary>
    /// <param name="discovers">Sorted discover participants.</param>
    /// <param name="allPlugins">Every registered plugin.</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every participant's discover hook has settled.</returns>
    private static async Task FireDiscoverAsync(
        IBuildDiscoverPlugin[] discovers,
        IPlugin[] allPlugins,
        BuildPhaseShell shell,
        CancellationToken cancellationToken)
    {
        if (discovers.Length is 0)
        {
            return;
        }

        var context = new BuildDiscoverContext(shell.InputRoot, shell.OutputRoot, allPlugins)
        {
            UseDirectoryUrls = shell.Options.UseDirectoryUrls,
        };
        var log = shell.Log;
        var pluginTiming = shell.PluginTiming;
        for (var i = 0; i < discovers.Length; i++)
        {
            var plugin = discovers[i];
            BuildPipelineLoggingHelper.LogPluginConfigure(log, plugin.Name);
            using (pluginTiming.Measure(plugin.Name))
            {
                await plugin.DiscoverAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Fires <see cref="IBuildResolvePlugin.ResolveAsync"/> on every plugin sorted by priority.</summary>
    /// <param name="resolves">Sorted resolve participants.</param>
    /// <param name="allPlugins">Every registered plugin.</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every participant's resolve hook has settled.</returns>
    private static async Task FireResolveAsync(
        IBuildResolvePlugin[] resolves,
        IPlugin[] allPlugins,
        BuildPhaseShell shell,
        CancellationToken cancellationToken)
    {
        if (resolves.Length is 0)
        {
            return;
        }

        var context = new BuildResolveContext(shell.OutputRoot, allPlugins);
        var log = shell.Log;
        var pluginTiming = shell.PluginTiming;
        for (var i = 0; i < resolves.Length; i++)
        {
            var plugin = resolves[i];
            BuildPipelineLoggingHelper.LogPluginFinalize(log, plugin.Name);
            using (pluginTiming.Measure(plugin.Name))
            {
                await plugin.ResolveAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Fires <see cref="IBuildFinalizePlugin.FinalizeAsync"/> on every plugin sorted by priority.</summary>
    /// <param name="finalizes">Sorted finalize participants.</param>
    /// <param name="allPlugins">Every registered plugin.</param>
    /// <param name="shell">Shared build-wide phase state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every participant's finalize hook has settled.</returns>
    private static async Task FireFinalizeAsync(
        IBuildFinalizePlugin[] finalizes,
        IPlugin[] allPlugins,
        BuildPhaseShell shell,
        CancellationToken cancellationToken)
    {
        if (finalizes.Length is 0)
        {
            return;
        }

        var context = new BuildFinalizeContext(shell.OutputRoot, allPlugins);
        var log = shell.Log;
        var pluginTiming = shell.PluginTiming;
        for (var i = 0; i < finalizes.Length; i++)
        {
            var plugin = finalizes[i];
            BuildPipelineLoggingHelper.LogPluginFinalize(log, plugin.Name);
            using (pluginTiming.Measure(plugin.Name))
            {
                await plugin.FinalizeAsync(context, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Bundle of per-page shared state threaded through <see cref="ProcessOnePageAsync"/>.</summary>
    /// <param name="Phases">Per-phase plugin arrays.</param>
    /// <param name="Previous">Previous-build manifest.</param>
    /// <param name="PluginTiming">Per-plugin time accumulator.</param>
    /// <param name="Buffered">Queue receiving rentals when cross-page resolution is needed.</param>
    private readonly record struct PerPageDispatch(
        PluginPhases Phases,
        BuildManifest Previous,
        PluginTimingTable PluginTiming,
        ConcurrentQueue<BufferedPage> Buffered);

    /// <summary>One page held back from immediate write because the build needs cross-page resolution.</summary>
    /// <param name="RelativePath">Source-relative path.</param>
    /// <param name="OutputPath">Absolute output path.</param>
    /// <param name="Rental">Pooled rental whose writer holds the post-render HTML; transferred ownership — the drain phase disposes it.</param>
    /// <param name="Hash">Source-content xxHash3 digest used as the manifest cache key.</param>
    private readonly record struct BufferedPage(
        FilePath RelativePath,
        FilePath OutputPath,
        PageBuilderRental Rental,
        byte[] Hash);

    /// <summary>Bundle of shared build-wide state threaded through fire helpers.</summary>
    /// <param name="InputRoot">Absolute input root.</param>
    /// <param name="OutputRoot">Absolute output root.</param>
    /// <param name="Options">Pipeline options.</param>
    /// <param name="PluginTiming">Per-plugin time accumulator.</param>
    /// <param name="Log">Logger.</param>
    private readonly record struct BuildPhaseShell(
        DirectoryPath InputRoot,
        DirectoryPath OutputRoot,
        BuildPipelineOptions Options,
        PluginTimingTable PluginTiming,
        ILogger Log);
}
