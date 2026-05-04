// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;
using NuStreamDocs.Privacy.Logging;
using Polly;

namespace NuStreamDocs.Privacy;

/// <summary>
/// Parallel HTTP downloader for the externalized assets registered
/// during <see cref="PrivacyPlugin.OnRenderPageAsync"/>.
/// </summary>
/// <remarks>
/// Iterates fixed-point: each pass downloads every URL the registry
/// holds; CSS files discovered along the way may register more nested
/// URLs (e.g. font files inside Google Fonts CSS) which the next pass
/// picks up. Capped to keep pathological CSS chains bounded. Each
/// fetch goes through a Polly retry pipeline so transient network
/// blips don't poison the result. A separate on-disk cache directory
/// holds the bytes across builds so subsequent runs skip the network
/// entirely.
/// </remarks>
internal static class ExternalAssetDownloader
{
    /// <summary>Maximum number of fixed-point download passes. CSS-inside-CSS chains are rare; three is plenty.</summary>
    private const int MaxIterations = 3;

    /// <summary>Minimum gap between in-flight progress beacons during a single iteration.</summary>
    private const long ProgressBeaconMillis = 5000;

    /// <summary>Initial backoff delay between retries.</summary>
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>Lifetime ceiling on a pooled connection — cap on how long DNS / TLS state stays warm before the handler force-recycles the socket.</summary>
    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(2);

    /// <summary>Idle ceiling on a pooled connection — drops sockets silent past this window so the pool releases promptly when the build's external-asset phase ends.</summary>
    private static readonly TimeSpan PooledConnectionIdleTimeout = TimeSpan.FromSeconds(15);

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
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentException.ThrowIfNullOrEmpty(outputRoot.Value);
        ArgumentException.ThrowIfNullOrEmpty(cacheRoot.Value);
        ArgumentOutOfRangeException.ThrowIfLessThan(settings.Parallelism, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(settings.MaxRetries);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(logger);

        using var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = PooledConnectionLifetime,
            PooledConnectionIdleTimeout = PooledConnectionIdleTimeout,
        };
        using var client = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = settings.Timeout,
        };
        var environment = new DownloadEnvironment(client, BuildRetryPipeline(settings.MaxRetries), registry, filter);

        var failures = new ConcurrentBag<string>();
        var processed = new HashSet<string>(StringComparer.Ordinal);
        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var snapshot = registry.EntriesSnapshot();
            var pendingBuffer = new List<(string Url, string LocalPath)>(snapshot.Length);
            for (var i = 0; i < snapshot.Length; i++)
            {
                // Decode at the use site — Uri.TryCreate + Path.Combine are the natural string boundary downstream.
                var url = Encoding.UTF8.GetString(snapshot[i].Url);
                if (processed.Add(url))
                {
                    pendingBuffer.Add((url, Encoding.UTF8.GetString(snapshot[i].LocalPath)));
                }
            }

            (string Url, string LocalPath)[] pending = [.. pendingBuffer];
            if (pending.Length is 0)
            {
                break;
            }

            var iterationNumber = iteration + 1;
            PrivacyLoggingHelper.LogIterationStart(logger, iterationNumber, pending.Length, settings.Parallelism);
            var iterationStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var iterationFailureBaseline = failures.Count;
            var completed = 0;
            var lastProgressTick = Environment.TickCount64;

            await Parallel.ForEachAsync(
                pending,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = settings.Parallelism,
                    CancellationToken = cancellationToken,
                },
                async (entry, ct) =>
                {
                    var target = new DownloadTarget(
                        entry.Url,
                        Path.Combine(outputRoot.Value, entry.LocalPath.Replace('/', Path.DirectorySeparatorChar)),
                        Path.Combine(cacheRoot.Value, entry.LocalPath.Replace('/', Path.DirectorySeparatorChar)));
                    if (!await DownloadOneAsync(environment, target, logger, ct).ConfigureAwait(false))
                    {
                        failures.Add(entry.Url);
                        PrivacyLoggingHelper.LogDownloadFailure(logger, entry.Url, target.OutputPath.Value);
                    }

                    var done = Interlocked.Increment(ref completed);

                    // Beacon every ~5 seconds: a single CAS guards the log so concurrent completions emit at most once per window.
                    var now = Environment.TickCount64;
                    var prev = Volatile.Read(ref lastProgressTick);
                    if (now - prev >= ProgressBeaconMillis &&
                        Interlocked.CompareExchange(ref lastProgressTick, now, prev) == prev)
                    {
                        PrivacyLoggingHelper.LogIterationProgress(logger, iterationNumber, done, pending.Length);
                    }
                })
                .ConfigureAwait(false);

            iterationStopwatch.Stop();
            PrivacyLoggingHelper.LogIterationComplete(
                logger,
                iterationNumber,
                pending.Length,
                failures.Count - iterationFailureBaseline,
                iterationStopwatch.Elapsed.TotalSeconds);
        }

        return [.. failures];
    }

    /// <summary>Builds a Polly resilience pipeline with exponential backoff for transient HTTP errors.</summary>
    /// <param name="maxRetries">Maximum retry attempts.</param>
    /// <returns>A configured <see cref="ResiliencePipeline{TResult}"/> over <see cref="HttpResponseMessage"/>.</returns>
    private static ResiliencePipeline<HttpResponseMessage> BuildRetryPipeline(int maxRetries) =>
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new()
            {
                MaxRetryAttempts = maxRetries,
                BackoffType = DelayBackoffType.Exponential,
                Delay = InitialRetryDelay,
                ShouldHandle = static args => ValueTask.FromResult(DownloadHttpClassifier.IsTransient(args.Outcome)),
            })
            .Build();

    /// <summary>Downloads <paramref name="target"/>'s URL through the retry pipeline, caches it, and copies it into the output root.</summary>
    /// <param name="environment">Shared per-batch environment.</param>
    /// <param name="target">Per-URL paths.</param>
    /// <param name="logger">Logger for cache-hit / download-success diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the file landed in the output root (cache hit or fresh fetch); false on any failure.</returns>
    private static async Task<bool> DownloadOneAsync(DownloadEnvironment environment, DownloadTarget target, ILogger logger, CancellationToken cancellationToken)
    {
        if (await TryCopyFromCacheAsync(target.CachePath, target.OutputPath, cancellationToken).ConfigureAwait(false))
        {
            PrivacyLoggingHelper.LogCacheHit(logger, target.Url, target.CachePath.Value);
            return true;
        }

        if (!Uri.TryCreate(target.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        try
        {
            using var response = await environment.Pipeline.ExecuteAsync(
                async ct => await environment.Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false),
                cancellationToken)
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

            await WriteCacheAndOutputAsync(target.CachePath, target.OutputPath, bytes, cancellationToken).ConfigureAwait(false);
            PrivacyLoggingHelper.LogDownloadSuccess(logger, target.Url, target.OutputPath.Value);
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
    }

    /// <summary>Copies <paramref name="cachePath"/> to <paramref name="outputPath"/> when the cache file exists.</summary>
    /// <param name="cachePath">Absolute cache file.</param>
    /// <param name="outputPath">Absolute output file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when the cache hit and the file was copied.</returns>
    private static async Task<bool> TryCopyFromCacheAsync(FilePath cachePath, FilePath outputPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(cachePath.Value))
        {
            return false;
        }

        Directory.CreateDirectory(outputPath.Directory.Value);
        await using var src = File.OpenRead(cachePath.Value);
        await using var dst = File.Create(outputPath.Value);
        await src.CopyToAsync(dst, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>Writes <paramref name="bytes"/> to both the cache and the output root.</summary>
    /// <param name="cachePath">Absolute cache file.</param>
    /// <param name="outputPath">Absolute output file.</param>
    /// <param name="bytes">Bytes to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when both writes finish.</returns>
    private static async Task WriteCacheAndOutputAsync(FilePath cachePath, FilePath outputPath, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(cachePath.Directory.Value);
        Directory.CreateDirectory(outputPath.Directory.Value);
        await File.WriteAllBytesAsync(cachePath.Value, bytes, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(outputPath.Value, bytes, cancellationToken).ConfigureAwait(false);
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
    public readonly record struct DownloadTarget(string Url, FilePath OutputPath, FilePath CachePath);

    /// <summary>Shared per-batch state passed through the download helpers.</summary>
    /// <param name="Client">Shared HTTP client.</param>
    /// <param name="Pipeline">Polly retry pipeline.</param>
    /// <param name="Registry">URL registry.</param>
    /// <param name="Filter">Host filter.</param>
    public readonly record struct DownloadEnvironment(
        HttpClient Client,
        ResiliencePipeline<HttpResponseMessage> Pipeline,
        ExternalAssetRegistry Registry,
        HostFilter Filter);
}
