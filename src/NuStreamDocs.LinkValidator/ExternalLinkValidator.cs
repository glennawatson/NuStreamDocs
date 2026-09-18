// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NuStreamDocs.Common;
using Polly;

namespace NuStreamDocs.LinkValidator;

/// <summary>HTTP-checks every absolute URL recorded in a <see cref="ValidationCorpus"/>.</summary>
public static class ExternalLinkValidator
{
    /// <summary>Initial bucket capacity for per-host hit lists.</summary>
    private const int HostBucketCapacity = 8;

    /// <summary>Validates every external URL in <paramref name="corpus"/>.</summary>
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
        options.Validate();

        var byHost = BucketByHost(corpus);
        if (byHost.Count == 0)
        {
            return [];
        }

        ConcurrentBag<LinkDiagnostic> diagnostics = [];
        List<Task> hostTasks = [with(byHost.Count)];
        foreach (var (host, urls) in byHost)
        {
            hostTasks.Add(ProcessHostAsync(host, urls, options, httpClient, diagnostics, cancellationToken));
        }

        await Task.WhenAll(hostTasks).ConfigureAwait(false);
        return [.. diagnostics];
    }

    /// <summary>Buckets every external URL in the corpus by host, recording each occurrence's source page.</summary>
    /// <param name="corpus">Corpus.</param>
    /// <returns>Map from host to per-URL hit list.</returns>
    internal static Dictionary<string, List<ExternalHit>> BucketByHost(ValidationCorpus corpus)
    {
        Dictionary<string, List<ExternalHit>> map = [with(StringComparer.OrdinalIgnoreCase)];
        for (var p = 0; p < corpus.Pages.Length; p++)
        {
            var page = corpus.Pages[p];
            string? sourcePageString = null;
            for (var i = 0; i < page.ExternalLinks.Length; i++)
            {
                var urlBytes = page.ExternalLinks[i];
                var url = Encoding.UTF8.GetString(urlBytes);
                if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
                {
                    continue;
                }

                sourcePageString ??= Encoding.UTF8.GetString(page.PageUrl);
                ref var bucket = ref CollectionsMarshal.GetValueRefOrAddDefault(map, parsed.Host, out _);
                bucket ??= [with(HostBucketCapacity)];
                bucket.Add(new(sourcePageString, url, parsed));
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
    [SuppressMessage(
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
        using SemaphoreSlim concurrency = new(options.MaxConcurrencyPerHost, options.MaxConcurrencyPerHost);
        List<Task> tasks = [with(hits.Count)];
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
                static async (state, ct) =>
                {
                    using HttpRequestMessage request = new(HttpMethod.Head, state.Hit.Uri);
                    request.Headers.UserAgent.ParseAdd(state.Options.UserAgent);

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(state.Options.RequestTimeoutSeconds));
                    using var response = await state.Client
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token)
                        .ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        state.Sink.Add(new(
                            state.Hit.SourcePage,
                            state.Hit.Url,
                            LinkSeverity.Error,
                            BuildHttpErrorMessage((int)response.StatusCode, response.ReasonPhrase, state.Hit.Url)));
                    }
                },
                (Hit: hit, Client: httpClient, Options: options, Sink: sink),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sink.Add(new(
                hit.SourcePage,
                hit.Url,
                LinkSeverity.Error,
                StringCompose.Concat(ex.GetType().Name, ": ", ex.Message)));
        }
        finally
        {
            _ = concurrency.Release();
        }
    }

    /// <summary>Builds a Polly pipeline combining sliding-window rate limiting with exponential-backoff retry.</summary>
    /// <param name="options">Validator options.</param>
    /// <returns>The configured pipeline.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ResiliencePipeline BuildPipeline(ExternalLinkValidatorOptions options) =>
        ExternalLinkPipelineFactory.Create(options);

    /// <summary>Composes the HTTP-error diagnostic message via <see cref="StringCompose"/> (one explicit allocation per leaf concat).</summary>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="reasonPhrase">HTTP reason phrase (may be null).</param>
    /// <param name="url">Source link URL.</param>
    /// <returns>Composed message.</returns>
    private static string BuildHttpErrorMessage(int statusCode, string? reasonPhrase, UrlPath url)
    {
        var reason = reasonPhrase ?? string.Empty;
        return StringCompose.ConcatInt(
            "HTTP ",
            statusCode,
            StringCompose.Concat(" ", reason, " for ", url.Value));
    }

    /// <summary>One external-URL occurrence: source page plus the parsed URI.</summary>
    /// <param name="SourcePage">Page URL the link came from.</param>
    /// <param name="Url">Raw URL.</param>
    /// <param name="Uri">Parsed URI.</param>
    internal readonly record struct ExternalHit(UrlPath SourcePage, UrlPath Url, Uri Uri);
}
