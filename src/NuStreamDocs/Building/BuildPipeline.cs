// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Caching;
using NuStreamDocs.Common;
using NuStreamDocs.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Building;

/// <summary>
/// Static driver that streams <see cref="PageWorkItem"/>s through the build pipeline phases
/// (Configure → Discover → parallel per-page render → cross-page Resolve barrier → drain buffered
/// pages → asset copy → manifest save → Finalize).
/// </summary>
public static class BuildPipeline
{
    /// <summary>Milliseconds per second for user-facing elapsed-time conversion.</summary>
    private const double MillisecondsPerSecond = 1000d;

    /// <summary>Runs the build with no cancellation support and default options.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <returns>The total number of pages processed (rendered + skipped).</returns>
    public static Task<int> RunAsync(in DirectoryPath inputRoot, in DirectoryPath outputRoot, IPlugin[] plugins) =>
        RunAsync(inputRoot, outputRoot, plugins, BuildPipelineOptions.Default, CancellationToken.None);

    /// <summary>Runs the build with cancellation support and default options.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of pages processed.</returns>
    public static Task<int> RunAsync(
        in DirectoryPath inputRoot,
        in DirectoryPath outputRoot,
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
        ValidateInputs(inputRoot, outputRoot);

        var filter = options.Filter ?? PathFilter.Empty;
        var useDirectoryUrls = options.UseDirectoryUrls;
        var includeDrafts = options.IncludeDrafts;

        var log = options.Logger ?? NullLogger.Instance;
        BuildPipelineLoggingHelper.LogBuildStart(log, inputRoot.Value, outputRoot.Value, plugins.Length);
        var stopwatch = Stopwatch.StartNew();
        PluginTimingTable pluginTiming = new();
        var buildFingerprint = BuildFingerprint.Create(plugins, options);

        Directory.CreateDirectory(outputRoot);
        var previous = await BuildManifest.LoadAsync(outputRoot, buildFingerprint, cancellationToken, log)
            .ConfigureAwait(false);

        // ConcurrentQueue's segmented linked-list outperforms ConcurrentBag for the
        // append-only-from-many-threads / drain-once pattern, and ToArray() is a
        // single right-sized allocation rather than the [.. bag] enumerator copy.
        ConcurrentQueue<ManifestEntry> fresh = new();
        ConcurrentQueue<BufferedPage> bufferedPages = new();

        // Partition into per-phase sorted arrays (one allocation per phase, once per build).
        var phases = PluginPhases.Partition(plugins);
        CrossPageMarkerRegistry crossPageMarkers = new();
        BuildPhaseShell shell = new(inputRoot, outputRoot, options, pluginTiming, log);

        var syntheticPages = await FireStartupPhasesAsync(phases, plugins, shell, crossPageMarkers, cancellationToken).ConfigureAwait(false);

        var processed = 0;
        var cacheHits = 0;
        ParallelOptions parallelOptions = new() { CancellationToken = cancellationToken, MaxDegreeOfParallelism = options.Parallelism };

        PerPageDispatch perPage = new(phases, previous, pluginTiming, bufferedPages, SnapshotMarkerNeedles(phases, crossPageMarkers));

        BuildPipelineLoggingHelper.LogRenderStart(log, parallelOptions.MaxDegreeOfParallelism);
        var renderStarted = stopwatch.ElapsedMilliseconds;
        await Parallel.ForEachAsync(
            BuildPipelinePageProcessor.EnumerateDiskAndSyntheticAsync(inputRoot, filter, syntheticPages, cancellationToken),
            parallelOptions,
            async (item, ct) =>
            {
                if (!includeDrafts && (item.Flags & PageFlags.Draft) != 0)
                {
                    return;
                }

                var (entry, hit, didBuffer) = await BuildPipelinePageProcessor.ProcessOnePageAsync(item, outputRoot, useDirectoryUrls, perPage, ct)
                    .ConfigureAwait(false);
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
            await BuildPipelinePluginOrchestrator.FireResolveAsync(phases.Resolves, plugins, shell, cancellationToken).ConfigureAwait(false);
            await BuildPipelinePageProcessor.DrainBufferedPagesAsync(bufferedPages, phases.PostResolves, fresh, shell, cancellationToken).ConfigureAwait(false);
        }

        // Copy author-supplied static content from docs/ to site/ — images, fonts, vendor JS,
        // anything the page templates or theme options reference by docs-relative path. Runs
        // before finalize so plugins like sitemap/search/privacy see the assets in place.
        var assetsCopied = DocsAssetCopier.Copy(inputRoot, outputRoot, filter);
        BuildPipelineLoggingHelper.LogAssetsCopied(log, assetsCopied);

        previous.Replace(fresh);
        await previous.SaveAsync(outputRoot, cancellationToken, log).ConfigureAwait(false);

        BuildPipelineLoggingHelper.LogFinalizeStart(log, phases.Finalizes.Length);
        await BuildPipelinePluginOrchestrator.FireFinalizeAsync(phases.Finalizes, plugins, shell, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        pluginTiming.Emit(log);
        BuildPipelineLoggingHelper.LogBuildComplete(log, processed, cacheHits, stopwatch.ElapsedMilliseconds / MillisecondsPerSecond);
        return processed;
    }

    /// <summary>Validates that the input and output roots are non-empty.</summary>
    /// <param name="inputRoot">The input directory root.</param>
    /// <param name="outputRoot">The output directory root.</param>
    private static void ValidateInputs(DirectoryPath inputRoot, DirectoryPath outputRoot)
    {
        if (inputRoot.IsEmpty)
        {
            throw new ArgumentException("Input root must be non-empty.", nameof(inputRoot));
        }

        if (!outputRoot.IsEmpty)
        {
            return;
        }

        throw new ArgumentException("Output root must be non-empty.", nameof(outputRoot));
    }

    /// <summary>Fires the configure and discover phases of the build pipeline.</summary>
    /// <param name="phases">The partitioned plugin phases.</param>
    /// <param name="plugins">The full set of registered plugins.</param>
    /// <param name="shell">The shared build phase shell.</param>
    /// <param name="crossPageMarkers">The registry for cross-page markers.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A sink populated during the discover phase with synthetic pages.</returns>
    private static async Task<SyntheticPageSink> FireStartupPhasesAsync(
        PluginPhases phases,
        IPlugin[] plugins,
        BuildPhaseShell shell,
        CrossPageMarkerRegistry crossPageMarkers,
        CancellationToken cancellationToken)
    {
        BuildPipelineLoggingHelper.LogConfigureStart(shell.Log, phases.Configures.Length);
        await BuildPipelinePluginOrchestrator.FireConfigureAsync(phases.Configures, plugins, shell, crossPageMarkers, cancellationToken)
            .ConfigureAwait(false);

        SyntheticPageSink syntheticPages = new();
        await BuildPipelinePluginOrchestrator.FireDiscoverAsync(phases.Discovers, plugins, shell, syntheticPages, cancellationToken)
            .ConfigureAwait(false);
        return syntheticPages;
    }

    /// <summary>Snapshots the registered cross-page marker needles for the per-page fast-path.</summary>
    /// <param name="phases">Per-phase plugin arrays.</param>
    /// <param name="registry">Live registry seeded during configure.</param>
    /// <returns>An empty array when no cross-page work is registered; otherwise the registered needle bytes.</returns>
    private static byte[][] SnapshotMarkerNeedles(PluginPhases phases, CrossPageMarkerRegistry registry) =>
        phases.NeedsCrossPageBarrier ? [.. registry.Markers] : [];
}
