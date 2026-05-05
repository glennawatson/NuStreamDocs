// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Threading.RateLimiting;
using Polly;
using Polly.RateLimiting;

namespace NuStreamDocs.LinkValidator;

/// <summary>Builds the Polly pipeline (sliding-window rate limit + exponential-backoff retry) used by <see cref="ExternalLinkValidator"/>.</summary>
internal static class ExternalLinkPipelineFactory
{
    /// <summary>Creates a configured pipeline for <paramref name="options"/>.</summary>
    /// <param name="options">Validator options.</param>
    /// <returns>The configured pipeline.</returns>
    public static ResiliencePipeline Create(ExternalLinkValidatorOptions options) =>
        new ResiliencePipelineBuilder()
            .AddRateLimiter(new RateLimiterStrategyOptions
            {
                RateLimiter = args => new SlidingWindowRateLimiter(new()
                {
                    PermitLimit = options.MaxRequestsPerHost,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    SegmentsPerWindow = Math.Max(1, options.WindowSeconds),
                    QueueLimit = int.MaxValue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                }).AcquireAsync(permitCount: 1, args.Context.CancellationToken)
            })
            .AddRetry(new()
            {
                MaxRetryAttempts = options.MaxRetries,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = static args => args.Outcome.Exception switch
                {
                    HttpRequestException or TaskCanceledException => PredicateResult.True(),
                    _ => PredicateResult.False()
                }
            })
            .Build();
}
