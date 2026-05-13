// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace NuStreamDocs.LinkValidator;

/// <summary>Shared parallel-fan-out for the per-page link validators.</summary>
internal static class LinkValidationRun
{
    /// <summary>Validates every page in <paramref name="corpus"/> in parallel and returns the combined diagnostics.</summary>
    /// <param name="corpus">The pre-built corpus.</param>
    /// <param name="parallelism">Maximum parallel page checks.</param>
    /// <param name="validatePage">Per-page validation step; receives the corpus, the page, and the shared diagnostic accumulator.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Diagnostics in arbitrary order.</returns>
    public static async Task<LinkDiagnostic[]> ForEachPageAsync(
        ValidationCorpus corpus,
        int parallelism,
        Action<ValidationCorpus, PageLinks, ConcurrentBag<LinkDiagnostic>> validatePage,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallelism);

        ConcurrentBag<LinkDiagnostic> diagnostics = [];
        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = parallelism
        };

        await Parallel.ForEachAsync(
            corpus.Pages,
            parallelOptions,
            (page, _) =>
            {
                validatePage(corpus, page, diagnostics);
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        return [.. diagnostics];
    }
}
