// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Polly;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// HTTP-checks every absolute URL recorded in a <see cref="ValidationCorpus"/>.
/// </summary>
/// <remarks>
/// Reads from the pre-built corpus only — no second filesystem walk.
/// Targets are bucketed by host; each bucket is processed through a
/// per-host Polly pipeline that combines a sliding-window rate
/// limiter with exponential-backoff retry. Buckets execute in
/// parallel so two slow hosts don't block each other.
/// </remarks>
public static class ExternalLinkValidator
{
    /// <summary>Initial bucket capacity for per-host hit lists; small constant since most hosts get a handful of links.</summary>
    private const int HostBucketCapacity = 8;

    /// <summary>Validates every external URL in <paramref name="corpus"/> through host-isolated Polly pipelines.</summary>
    /// <param name="corpus">The pre-built corpus.</param>
    /// <param name="options">Rate-limit + retry configuration.</param>
    /// <param name="httpClient">HTTP client to use; the caller owns its lifetime.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Diagnostics in arbitrary order.</returns>
    public static async Task<LinkDiagnostic[]> ValidateAsync(
        ValidationCorpus corpus,
        ExternalLinkValidatorOptions options,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(httpClient);
        options.Validate();

        var byHost = BucketByHost(corpus);
        if (byHost.Count == 0)
        {
            return [];
        }

        var diagnostics = new ConcurrentBag<LinkDiagnostic>();
        var hostTasks = new List<Task>(byHost.Count);
        foreach (var (host, urls) in byHost)
        {
            hostTasks.Add(ProcessHostAsync(host, urls, options, httpClient, diagnostics, cancellationToken));
        }

        await Task.WhenAll(hostTasks).ConfigureAwait(false);
        return [.. diagnostics];
    }

    /// <summary>Buckets every external URL in the corpus by its host, recording each occurrence's source page.</summary>
    /// <param name="corpus">Corpus.</param>
    /// <returns>Map from host to per-URL hit list.</returns>
    internal static Dictionary<string, List<ExternalHit>> BucketByHost(ValidationCorpus corpus)
    {
        var map = new Dictionary<string, List<ExternalHit>>(StringComparer.OrdinalIgnoreCase);
        for (var p = 0; p < corpus.Pages.Length; p++)
        {
            var page = corpus.Pages[p];
            for (var i = 0; i < page.ExternalLinks.Length; i++)
            {
                var url = page.ExternalLinks[i];
                if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
                {
                    continue;
                }

                var host = parsed.Host;
                if (!map.TryGetValue(host, out var bucket))
                {
                    bucket = new(HostBucketCapacity);
                    map[host] = bucket;
                }

                bucket.Add(new(page.PageUrl, url, parsed));
            }
        }

        return map;
    }

    /// <summary>Runs the per-host pipeline against every URL in the bucket.</summary>
    /// <param name="host">Host name (used only for diagnostics).</param>
    /// <param name="hits">URLs targeting this host.</param>
    /// <param name="options">Validator options.</param>
    /// <param name="httpClient">Shared HTTP client.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the per-host run.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Reliability",
        "CA2025:Ensure tasks using 'IDisposable' instances complete before the instances are disposed",
        Justification = "Task.WhenAll(tasks) is awaited inside the using scope, so the SemaphoreSlim lives until every per-URL task has completed.")]
    private static async Task ProcessHostAsync(
        string host,
        List<ExternalHit> hits,
        ExternalLinkValidatorOptions options,
        HttpClient httpClient,
        ConcurrentBag<LinkDiagnostic> sink,
        CancellationToken cancellationToken)
    {
        _ = host;
        var pipeline = BuildPipeline(options);
        using var concurrency = new SemaphoreSlim(options.MaxConcurrencyPerHost, options.MaxConcurrencyPerHost);
        var tasks = new List<Task>(hits.Count);
        for (var i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            tasks.Add(CheckOneAsync(hit, pipeline, httpClient, options, concurrency, sink, cancellationToken));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>Issues one HEAD request through the pipeline and records the result.</summary>
    /// <param name="hit">Source-page + URL pair.</param>
    /// <param name="pipeline">Polly pipeline.</param>
    /// <param name="httpClient">HTTP client.</param>
    /// <param name="options">Validator options.</param>
    /// <param name="concurrency">Per-host concurrency gate.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the request.</returns>
    private static async Task CheckOneAsync(
        ExternalHit hit,
        ResiliencePipeline pipeline,
        HttpClient httpClient,
        ExternalLinkValidatorOptions options,
        SemaphoreSlim concurrency,
        ConcurrentBag<LinkDiagnostic> sink,
        CancellationToken cancellationToken)
    {
        await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await pipeline.ExecuteAsync(
                async ct =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, hit.Uri);
                    request.Headers.UserAgent.ParseAdd(options.UserAgent);

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));
                    using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        sink.Add(new(hit.SourcePage, hit.Url, LinkSeverity.Error, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} for {hit.Url}"));
                    }
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sink.Add(new(hit.SourcePage, hit.Url, LinkSeverity.Error, $"{ex.GetType().Name}: {ex.Message}"));
        }
        finally
        {
            concurrency.Release();
        }
    }

    /// <summary>Builds a Polly pipeline combining sliding-window rate limiting with exponential-backoff retry.</summary>
    /// <param name="options">Validator options.</param>
    /// <returns>The configured pipeline.</returns>
    private static ResiliencePipeline BuildPipeline(ExternalLinkValidatorOptions options) =>
        ExternalLinkPipelineFactory.Create(options);

    /// <summary>One external-URL occurrence: source page plus the parsed URI.</summary>
    /// <param name="SourcePage">Page URL the link came from.</param>
    /// <param name="Url">Raw URL string.</param>
    /// <param name="Uri">Parsed URI.</param>
    internal readonly record struct ExternalHit(string SourcePage, string Url, Uri Uri);
}
