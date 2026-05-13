// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using NuStreamDocs.Common;
using NuStreamDocs.Privacy.Logging;
using Polly;
using Polly.RateLimiting;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Parallel HTTP downloader for the externalized assets registered during
/// <see cref="PrivacyPlugin.PostRender"/>; iterates to a fixed point so nested CSS-discovered URLs
/// are picked up.
/// </summary>
internal static class ExternalAssetDownloader
{
    /// <summary>Maximum number of fixed-point download passes. CSS-inside-CSS chains are rare; three is plenty.</summary>
    private const int MaxIterations = 3;

    /// <summary>Minimum gap between in-flight progress beacons during a single iteration.</summary>
    private const long ProgressBeaconMillis = 5000;

    /// <summary>Per-host in-flight request ceiling.</summary>
    private const int MaxConcurrencyPerHost = 4;

    /// <summary>Initial backoff delay between retries.</summary>
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>Lifetime ceiling on a pooled connection.</summary>
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

    /// <summary>Idle ceiling on a pooled connection.</summary>
    private static readonly TimeSpan PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Resilience-context property key carrying the destination host for per-host rate limit partitioning.</summary>
    private static readonly ResiliencePropertyKey<string> HostPropertyKey = new("nustreamdocs.privacy.host");

    /// <summary>Downloads every URL the <paramref name="registry"/> holds, iterating until the registry stops growing or the cap is reached.</summary>
    /// <param name="registry">URL registry; mutated as nested URLs are discovered.</param>
    /// <param name="outputRoot">Absolute output root.</param>
    /// <param name="cacheRoot">Absolute on-disk cache root; downloaded bytes land here first and are copied into <paramref name="outputRoot"/>.</param>
    /// <param name="settings">Per-batch parallelism, timeout, retry settings.</param>
    /// <param name="filter">Host filter; CSS post-processing applies it to nested URLs.</param>
    /// <param name="logger">Logger for per-download diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of URLs that exhausted retries and never succeeded.</returns>
    public static async Task<string[]> DownloadAllAsync(
        ExternalAssetRegistry registry,
        DirectoryPath outputRoot,
        DirectoryPath cacheRoot,
        DownloadSettings settings,
        HostFilter filter,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot.Value);
        ArgumentException.ThrowIfNullOrEmpty(cacheRoot.Value);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.Parallelism, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MaxRetries);

        using SocketsHttpHandler handler = new();
        handler.PooledConnectionLifetime = PooledConnectionLifetime;
        handler.PooledConnectionIdleTimeout = PooledConnectionIdleTimeout;
        handler.EnableMultipleHttp2Connections = true;
        using HttpClient client = new(handler, false);
        client.Timeout = settings.Timeout;
        client.DefaultRequestVersion = HttpVersion.Version20;
        client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        await using var perHostLimiter = BuildPerHostLimiter();
        DownloadEnvironment environment =
            new(client, BuildPipeline(settings.MaxRetries, perHostLimiter), registry, filter);

        ConcurrentBag<string> failures = [];
        HashSet<byte[]> processed = new(ByteArrayComparer.Instance);
        IterationContext iterationContext = new(environment, settings, outputRoot, cacheRoot, failures, logger);
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var pending = SnapshotPending(registry, processed);
            if (pending.Length is 0)
            {
                break;
            }

            await RunIterationAsync(iteration + 1, pending, iterationContext, cancellationToken).ConfigureAwait(false);
        }

        return [.. failures];
    }

    /// <summary>Snapshots the registry into a fresh pending-list of URLs not yet seen by an earlier pass.</summary>
    /// <param name="registry">URL registry; mutated as nested CSS URLs are discovered.</param>
    /// <param name="processed">Cross-iteration dedupe set keyed on UTF-8 URL bytes; updated in place.</param>
    /// <returns>Pending entries to download in this pass.</returns>
    private static (UrlPath Url, FilePath LocalPath)[] SnapshotPending(
        ExternalAssetRegistry registry,
        HashSet<byte[]> processed)
    {
        var snapshot = registry.EntriesSnapshot();
        List<(UrlPath Url, FilePath LocalPath)> pendingBuffer = new(snapshot.Length);
        for (var i = 0; i < snapshot.Length; i++)
        {
            // Skip the UTF-8 decode for entries we've already processed in an earlier pass — only decode when we're actually queuing the URL.
            if (processed.Add(snapshot[i].Url))
            {
                pendingBuffer.Add((new(Encoding.UTF8.GetString(snapshot[i].Url)),
                    new(Encoding.UTF8.GetString(snapshot[i].LocalPath))));
            }
        }

        return [.. pendingBuffer];
    }

    /// <summary>Runs one fixed-point pass — parallel-downloads <paramref name="pending"/> and emits start/progress/complete diagnostics.</summary>
    /// <param name="iterationNumber">1-based iteration counter for log messages.</param>
    /// <param name="pending">Pending entries collected by <see cref="SnapshotPending"/>.</param>
    /// <param name="ctx">Cross-iteration shared state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every parallel download in this pass finishes.</returns>
    private static async Task RunIterationAsync(
        int iterationNumber,
        (UrlPath Url, FilePath LocalPath)[] pending,
        IterationContext ctx,
        CancellationToken cancellationToken)
    {
        PrivacyLoggingHelper.LogIterationStart(ctx.Logger, iterationNumber, pending.Length, ctx.Settings.Parallelism);
        var iterationStopwatch = Stopwatch.StartNew();
        var iterationFailureBaseline = ctx.Failures.Count;
        var completed = 0;
        var lastProgressTick = Environment.TickCount64;

        await Parallel.ForEachAsync(
                pending,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = ctx.Settings.Parallelism,
                    CancellationToken = cancellationToken
                },
                async (entry, ct) =>
                {
                    var localPath = entry.LocalPath.Value.Replace('/', Path.DirectorySeparatorChar);
                    DownloadTarget target = new(
                        entry.Url,
                        Path.Combine(ctx.OutputRoot.Value, localPath),
                        Path.Combine(ctx.CacheRoot.Value, localPath));
                    if (!await DownloadOneAsync(ctx.Environment, target, ctx.Logger, ct).ConfigureAwait(false))
                    {
                        ctx.Failures.Add(entry.Url.Value);
                        PrivacyLoggingHelper.LogDownloadFailure(ctx.Logger, entry.Url.Value, target.OutputPath.Value);
                    }

                    var done = Interlocked.Increment(ref completed);

                    // Beacon every ~5 seconds: a single CAS guards the log so concurrent completions emit at most once per window.
                    var now = Environment.TickCount64;
                    var prev = Volatile.Read(ref lastProgressTick);
                    if (now - prev >= ProgressBeaconMillis &&
                        Interlocked.CompareExchange(ref lastProgressTick, now, prev) == prev)
                    {
                        PrivacyLoggingHelper.LogIterationProgress(ctx.Logger, iterationNumber, done, pending.Length);
                    }
                })
            .ConfigureAwait(false);

        iterationStopwatch.Stop();
        PrivacyLoggingHelper.LogIterationComplete(
            ctx.Logger,
            iterationNumber,
            pending.Length,
            ctx.Failures.Count - iterationFailureBaseline,
            iterationStopwatch.Elapsed.TotalSeconds);
    }

    /// <summary>Builds a partitioned concurrency limiter — one <see cref="ConcurrencyLimiter"/> per host with <see cref="MaxConcurrencyPerHost"/> permits.</summary>
    /// <returns>A disposable partitioned limiter ready to plug into <see cref="RateLimiterStrategyOptions"/>.</returns>
    private static PartitionedRateLimiter<ResilienceContext> BuildPerHostLimiter() =>
        PartitionedRateLimiter.Create<ResilienceContext, string>(static ctx =>
        {
            var host = ctx.Properties.GetValue(HostPropertyKey, string.Empty);
            return RateLimitPartition.GetConcurrencyLimiter(
                host,
                static _ => new() { PermitLimit = MaxConcurrencyPerHost, QueueLimit = int.MaxValue });
        });

    /// <summary>Builds a Polly resilience pipeline with per-host concurrency limiting and exponential-backoff retry on transient HTTP errors.</summary>
    /// <param name="maxRetries">Maximum retry attempts.</param>
    /// <param name="perHostLimiter">Host-partitioned concurrency limiter shared across the batch.</param>
    /// <returns>A configured <see cref="ResiliencePipeline{TResult}"/> over <see cref="HttpResponseMessage"/>.</returns>
    private static ResiliencePipeline<HttpResponseMessage> BuildPipeline(
        int maxRetries,
        PartitionedRateLimiter<ResilienceContext> perHostLimiter) =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRateLimiter(new RateLimiterStrategyOptions
            {
                RateLimiter = args => perHostLimiter.AcquireAsync(args.Context, 1, args.Context.CancellationToken)
            })
            .AddRetry(new()
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = InitialRetryDelay,
                ShouldHandle = static args => ValueTask.FromResult(DownloadHttpClassifier.IsTransient(args.Outcome))
            })
            .Build();

    /// <summary>Downloads <paramref name="target"/>'s URL through the retry pipeline, caches it, and copies it into the output root.</summary>
    /// <param name="environment">Shared per-batch environment.</param>
    /// <param name="target">Per-URL paths.</param>
    /// <param name="logger">Logger for cache-hit / download-success diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the file landed in the output root (cache hit or fresh fetch); false on any failure.</returns>
    private static async Task<bool> DownloadOneAsync(
        DownloadEnvironment environment,
        DownloadTarget target,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (TryPublishFromCache(target.CachePath, target.OutputPath))
        {
            PrivacyLoggingHelper.LogCacheHit(logger, target.Url.Value, target.CachePath.Value);
            return true;
        }

        if (!Uri.TryCreate(target.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(HostPropertyKey, uri.Host);
        try
        {
            using var response = await environment.Pipeline.ExecuteAsync(
                    static async (ctx, state) => await state.Client
                        .GetAsync(state.Uri, HttpCompletionOption.ResponseHeadersRead, ctx.CancellationToken)
                        .ConfigureAwait(false),
                    context,
                    new GetCallState(environment.Client, uri))
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (DownloadHttpClassifier.LooksLikeCss(uri, response))
            {
                bytes = CssUrlRewriter.Rewrite(bytes, uri, environment.Registry, environment.Filter);
            }

            await WriteCacheAndOutputAsync(target.CachePath, target.OutputPath, bytes, cancellationToken)
                .ConfigureAwait(false);
            PrivacyLoggingHelper.LogDownloadSuccess(logger, target.Url.Value, target.OutputPath.Value);
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false;
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }

    /// <summary>Publishes the cached asset at <paramref name="outputPath"/> when the cache file exists.</summary>
    /// <param name="cachePath">Absolute cache file.</param>
    /// <param name="outputPath">Absolute output file.</param>
    /// <returns>True when the cache hit and the file was published into the output tree.</returns>
    private static bool TryPublishFromCache(in FilePath cachePath, in FilePath outputPath)
    {
        if (!cachePath.Exists())
        {
            return false;
        }

        outputPath.Directory.Create();
        File.Copy(cachePath.Value, outputPath.Value, true);
        return true;
    }

    /// <summary>Writes <paramref name="bytes"/> to <paramref name="cachePath"/> and copies the cache file to <paramref name="outputPath"/>.</summary>
    /// <param name="cachePath">Absolute cache file (single source of truth on disk).</param>
    /// <param name="outputPath">Absolute output file.</param>
    /// <param name="bytes">Bytes to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the cache write + output copy finish.</returns>
    private static async Task WriteCacheAndOutputAsync(
        FilePath cachePath,
        FilePath outputPath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        cachePath.Directory.Create();
        await File.WriteAllBytesAsync(cachePath.Value, bytes, cancellationToken).ConfigureAwait(false);
        outputPath.Directory.Create();
        File.Copy(cachePath.Value, outputPath.Value, true);
    }

    /// <summary>Per-batch download tuning settings collapsed into one parameter.</summary>
    /// <param name="Parallelism">Maximum concurrent downloads.</param>
    /// <param name="Timeout">Per-request HTTP timeout.</param>
    /// <param name="MaxRetries">Maximum retry attempts on transient failures.</param>
    public readonly record struct DownloadSettings(int Parallelism, TimeSpan Timeout, int MaxRetries);

    /// <summary>Per-URL paths.</summary>
    /// <param name="Url">External URL.</param>
    /// <param name="OutputPath">Absolute output path.</param>
    /// <param name="CachePath">Absolute cache path.</param>
    public readonly record struct DownloadTarget(UrlPath Url, FilePath OutputPath, FilePath CachePath);

    /// <summary>Shared per-batch state passed through the download helpers.</summary>
    /// <param name="Client">Shared HTTP client.</param>
    /// <param name="Pipeline">Polly retry + per-host concurrency pipeline.</param>
    /// <param name="Registry">URL registry.</param>
    /// <param name="Filter">Host filter.</param>
    public readonly record struct DownloadEnvironment(
        HttpClient Client,
        ResiliencePipeline<HttpResponseMessage> Pipeline,
        ExternalAssetRegistry Registry,
        HostFilter Filter);

    /// <summary>State bundle for the <c>ResiliencePipeline.ExecuteAsync</c> static callback.</summary>
    /// <param name="Client">Shared HTTP client.</param>
    /// <param name="Uri">Absolute target URI.</param>
    private readonly record struct GetCallState(HttpClient Client, Uri Uri);

    /// <summary>Cross-iteration shared state passed into <see cref="RunIterationAsync"/>.</summary>
    /// <param name="Environment">Shared per-batch download environment.</param>
    /// <param name="Settings">Per-batch parallelism / timeout / retry settings.</param>
    /// <param name="OutputRoot">Absolute output root.</param>
    /// <param name="CacheRoot">Absolute on-disk cache root.</param>
    /// <param name="Failures">Cross-iteration failure sink.</param>
    /// <param name="Logger">Logger for per-download diagnostics.</param>
    private readonly record struct IterationContext(
        DownloadEnvironment Environment,
        DownloadSettings Settings,
        DirectoryPath OutputRoot,
        DirectoryPath CacheRoot,
        ConcurrentBag<string> Failures,
        ILogger Logger);
}
