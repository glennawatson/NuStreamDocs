// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Caching;
using NuStreamDocs.Logging;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Building;

/// <summary>
/// Static driver that streams <see cref="PageWorkItem"/>s through the
/// per-page parse, render, plugin and write stages.
/// </summary>
/// <remarks>
/// Memory stays proportional to the parallel-worker count, never the
/// corpus size. Each worker:
/// <list type="number">
/// <item>reads the source file and computes its xxHash3 digest,</item>
/// <item>looks up the previous-build manifest entry — when the hash
/// matches and the cached output file is still on disk, the entry is
/// kept verbatim and the rest of the per-page pipeline is skipped,</item>
/// <item>otherwise rents a UTF-8 output writer from <see cref="PageBuilderPool"/>,</item>
/// <item>runs the static <see cref="MarkdownRenderer"/>,</item>
/// <item>fires <see cref="IDocPlugin.OnRenderPageAsync"/> on every plugin,</item>
/// <item>writes the bytes to the output path and records the new entry.</item>
/// </list>
/// Plugin <c>OnConfigure</c> + <c>OnFinalise</c> are invoked once,
/// outside the parallel section.
/// <para>
/// The freshly-recorded entries replace the on-disk manifest at the
/// end of the build. Pages that were skipped keep their previous
/// entry; pages that were re-rendered overwrite theirs; pages that no
/// longer exist drop out. The manifest is intentionally idempotent —
/// deleting it forces a full rebuild.
/// </para>
/// </remarks>
public static class BuildPipeline
{
    /// <summary>Default parallel-worker cap; tuned for laptops and CI runners.</summary>
    private const int DefaultParallelism = 8;

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
    public static Task<int> RunAsync(string inputRoot, string outputRoot, IDocPlugin[] plugins) =>
        RunAsync(inputRoot, outputRoot, plugins, BuildPipelineOptions.Default, CancellationToken.None);

    /// <summary>Runs the build with cancellation support and default options.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of pages processed.</returns>
    public static Task<int> RunAsync(
        string inputRoot,
        string outputRoot,
        IDocPlugin[] plugins,
        CancellationToken cancellationToken) =>
        RunAsync(inputRoot, outputRoot, plugins, BuildPipelineOptions.Default, cancellationToken);

    /// <summary>Canonical build entry point: runs the pipeline with explicit <paramref name="options"/>.</summary>
    /// <param name="inputRoot">Absolute path to the docs root.</param>
    /// <param name="outputRoot">Absolute path to the site output root.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="options">Pipeline options (filter, logger, URL shape, draft toggle).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The total number of pages processed.</returns>
    public static async Task<int> RunAsync(
        string inputRoot,
        string outputRoot,
        IDocPlugin[] plugins,
        BuildPipelineOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputRoot);
        ArgumentException.ThrowIfNullOrEmpty(outputRoot);
        ArgumentNullException.ThrowIfNull(plugins);
        var filter = options.Filter ?? PathFilter.Empty;
        var useDirectoryUrls = options.UseDirectoryUrls;
        var includeDrafts = options.IncludeDrafts;

        var log = options.Logger ?? NullLogger.Instance;
        BuildPipelineLoggingHelper.LogBuildStart(log, inputRoot, outputRoot, plugins.Length);
        var stopwatch = Stopwatch.StartNew();

        Directory.CreateDirectory(outputRoot);
        var previous = await BuildManifest.LoadAsync(outputRoot, cancellationToken, log).ConfigureAwait(false);

        // ConcurrentQueue's segmented linked-list outperforms ConcurrentBag for the
        // append-only-from-many-threads / drain-once pattern, and ToArray() is a
        // single right-sized allocation rather than the [.. bag] enumerator copy.
        var fresh = new ConcurrentQueue<ManifestEntry>();

        await FireOnConfigureAsync(plugins, inputRoot, outputRoot, cancellationToken).ConfigureAwait(false);

        var processed = 0;
        var cacheHits = 0;
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = DefaultParallelism,
        };

        await Parallel.ForEachAsync(
            PageDiscovery.EnumerateAsync(inputRoot, filter, cancellationToken),
            parallelOptions,
            async (item, ct) =>
            {
                if (!includeDrafts && (item.Flags & PageFlags.Draft) != 0)
                {
                    return;
                }

                var (entry, hit) = await ProcessOnePageAsync(item, outputRoot, plugins, previous, useDirectoryUrls, ct).ConfigureAwait(false);
                fresh.Enqueue(entry);
                Interlocked.Increment(ref processed);
                if (hit)
                {
                    Interlocked.Increment(ref cacheHits);
                }

                BuildPipelineLoggingHelper.LogPageProcessed(log, item.RelativePath, hit);
            }).ConfigureAwait(false);

        previous.Replace(fresh);
        await previous.SaveAsync(outputRoot, cancellationToken, log).ConfigureAwait(false);

        await FireOnFinaliseAsync(plugins, outputRoot, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        BuildPipelineLoggingHelper.LogBuildComplete(log, processed, cacheHits, stopwatch.ElapsedMilliseconds);
        return processed;
    }

    /// <summary>Renders one page or short-circuits via the manifest.</summary>
    /// <param name="item">Page work item.</param>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="previous">Previous-build manifest.</param>
    /// <param name="useDirectoryUrls">Selects the output-path shape (flat <c>foo.html</c> vs <c>foo/index.html</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The fresh manifest entry for this page and a flag indicating whether the entry was a cache hit.</returns>
    /// <remarks>
    /// One state machine for the whole per-page pipeline. The hash check
    /// short-circuits before we rent any pooled buffer, so the cache-hit
    /// path stays cheap; the render path awaits each plugin's
    /// <see cref="IDocPlugin.OnRenderPageAsync"/> via <see cref="ValueTask"/>
    /// — most plugins return synchronously, so the average extra work
    /// per page is a no-op state-machine resumption.
    /// </remarks>
    private static async ValueTask<(ManifestEntry Entry, bool CacheHit)> ProcessOnePageAsync(
        PageWorkItem item,
        string outputRoot,
        IDocPlugin[] plugins,
        BuildManifest previous,
        bool useDirectoryUrls,
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
            var hash = ContentHasher.HashHex(source.Span);
            var outputPath = OutputPathFor(outputRoot, item.RelativePath, useDirectoryUrls);

            if (previous.TryGet(item.RelativePath, out var stale) &&
                string.Equals(stale.ContentHash, hash, StringComparison.Ordinal) &&
                File.Exists(outputPath))
            {
                // Hot incremental path: the source bytes match the previous
                // build and the output is still on disk. Keep the entry
                // unchanged so the next manifest write preserves it; skip
                // render, plugin hooks, and the file write.
                return (stale, true);
            }

            using var rental = PageBuilderPool.Rent(source.Length * 2);
            var writer = rental.Writer;
            var scratchRentals = new List<PageBuilderRental>();
            try
            {
                var processed = ApplyPreprocessors(source, plugins, scratchRentals, item.RelativePath);
                MarkdownRenderer.Render(processed.Span, writer);

                var context = new PluginRenderContext(item.RelativePath, source, writer);
                for (var i = 0; i < plugins.Length; i++)
                {
                    await plugins[i].OnRenderPageAsync(context, cancellationToken).ConfigureAwait(false);
                }

                EnsureDirectory(outputPath);

                // Sync write skips the BufferedFileStreamStrategy + ThreadPoolValueTaskSource
                // alloc chain (~308 MB on a 13.8K-page baseline run). Page bytes are already
                // in memory; there's nothing for async I/O to overlap with at this point.
                File.WriteAllBytes(outputPath, writer.WrittenSpan);

                return (new(item.RelativePath, hash, writer.WrittenCount), false);
            }
            finally
            {
                for (var i = 0; i < scratchRentals.Count; i++)
                {
                    scratchRentals[i].Dispose();
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sourceBuffer);
        }
    }

    /// <summary>Ensures the directory containing <paramref name="outputPath"/> exists, with a per-thread last-seen cache so repeat directories cost nothing.</summary>
    /// <param name="outputPath">Absolute output path.</param>
    /// <remarks>
    /// Path.GetDirectoryName(string) allocates a fresh string; the
    /// ReadOnlySpan overload doesn't. Pages in the same directory share
    /// the cache slot, so 13K pages spread over ~100 directories hit
    /// Directory.CreateDirectory at most once per directory per thread.
    /// </remarks>
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

    /// <summary>Threads <paramref name="source"/> through every plugin that implements <see cref="IMarkdownPreprocessor"/>.</summary>
    /// <param name="source">UTF-8 markdown bytes as read from disk.</param>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="scratch">Output rental list — the caller must dispose every entry to return the writers to <see cref="PageBuilderPool"/>.</param>
    /// <param name="relativePath">Page path relative to the input root; passed to path-aware preprocessors (frontmatter inheritance, metadata injection).</param>
    /// <returns>The rewritten bytes, or <paramref name="source"/> unchanged when no preprocessors are registered.</returns>
    private static ReadOnlyMemory<byte> ApplyPreprocessors(in ReadOnlyMemory<byte> source, IDocPlugin[] plugins, List<PageBuilderRental> scratch, string relativePath)
    {
        // We need at most two pool rentals: one to write the next pass into, one
        // holding the previous pass's bytes. With a single preprocessor registered
        // we don't ping-pong at all — count first so we can size rentals to fit.
        var preprocessorCount = CountPreprocessors(plugins);
        if (preprocessorCount is 0)
        {
            return source;
        }

        var capacity = source.Length * PreprocessorScratchMultiplier;
        var front = PageBuilderPool.Rent(capacity);
        scratch.Add(front);

        PageBuilderRental? back = null;
        if (preprocessorCount > 1)
        {
            var backRental = PageBuilderPool.Rent(capacity);
            scratch.Add(backRental);
            back = backRental;
        }

        var current = source;
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is not IMarkdownPreprocessor pre)
            {
                continue;
            }

            front.Writer.ResetWrittenCount();
            pre.Preprocess(current.Span, front.Writer, relativePath);
            current = front.Writer.WrittenMemory;

            if (back is null)
            {
                continue;
            }

            (front, back) = (back.Value, front);
        }

        return current;
    }

    /// <summary>Counts how many of <paramref name="plugins"/> implement <see cref="IMarkdownPreprocessor"/>.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <returns>Preprocessor count.</returns>
    private static int CountPreprocessors(IDocPlugin[] plugins)
    {
        var count = 0;
        for (var i = 0; i < plugins.Length; i++)
        {
            if (plugins[i] is IMarkdownPreprocessor)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Computes the on-disk output path for a relative source path.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="relativePath">Source-relative path.</param>
    /// <param name="useDirectoryUrls">When true, emits non-index pages as <c>foo/index.html</c>; when false, emits as <c>foo.html</c>.</param>
    /// <returns>The absolute output path with the <c>.html</c> extension.</returns>
    private static string OutputPathFor(string outputRoot, string relativePath, bool useDirectoryUrls) =>
        useDirectoryUrls
            ? OutputPathForDirectoryUrls(outputRoot, relativePath)
            : OutputPathForFlatUrls(outputRoot, relativePath);

    /// <summary>Flat-URL form: <c>foo.md</c> → <c>foo.html</c>; everything else passes through.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="relativePath">Source-relative path.</param>
    /// <returns>The absolute output path.</returns>
    private static string OutputPathForFlatUrls(string outputRoot, string relativePath) =>
        OutputPathBuilder.ForFlatUrls(outputRoot, relativePath);

    /// <summary>Directory-URL form: <c>guide/foo.md</c> → <c>guide/foo/index.html</c>; <c>guide/index.md</c> stays as <c>guide/index.html</c>.</summary>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="relativePath">Source-relative path.</param>
    /// <returns>The absolute output path.</returns>
    private static string OutputPathForDirectoryUrls(string outputRoot, string relativePath) =>
        OutputPathBuilder.ForDirectoryUrls(outputRoot, relativePath);

    /// <summary>Fires <see cref="IDocPlugin.OnConfigureAsync"/> on every plugin.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="inputRoot">Absolute input root.</param>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every plugin's configure hook has settled.</returns>
    private static async Task FireOnConfigureAsync(IDocPlugin[] plugins, string inputRoot, string outputRoot, CancellationToken cancellationToken)
    {
        var context = new PluginConfigureContext(default, inputRoot, outputRoot, plugins);
        for (var i = 0; i < plugins.Length; i++)
        {
            await plugins[i].OnConfigureAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Fires <see cref="IDocPlugin.OnFinaliseAsync"/> on every plugin.</summary>
    /// <param name="plugins">Registered plugins.</param>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every plugin's finalise hook has settled.</returns>
    private static async Task FireOnFinaliseAsync(IDocPlugin[] plugins, string outputRoot, CancellationToken cancellationToken)
    {
        var context = new PluginFinaliseContext(outputRoot);
        for (var i = 0; i < plugins.Length; i++)
        {
            await plugins[i].OnFinaliseAsync(context, cancellationToken).ConfigureAwait(false);
        }
    }
}
