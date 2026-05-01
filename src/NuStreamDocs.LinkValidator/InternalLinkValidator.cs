// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Walks a <see cref="ValidationCorpus"/> in parallel and reports
/// missing internal pages or unresolved <c>#fragment</c> anchors.
/// </summary>
/// <remarks>
/// Mirrors the surface of <c>mkdocs --strict</c>: relative-link
/// resolution and same-page heading-anchor checking, plus
/// cross-page anchor resolution on top. Reads only from the
/// already-populated corpus — no disk I/O.
/// </remarks>
public static class InternalLinkValidator
{
    /// <summary>Runs the validator and returns the full diagnostic set.</summary>
    /// <param name="corpus">The pre-built corpus.</param>
    /// <param name="parallelism">Maximum parallel page checks.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Diagnostics in arbitrary order.</returns>
    public static async Task<LinkDiagnostic[]> ValidateAsync(ValidationCorpus corpus, int parallelism, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallelism);

        var diagnostics = new ConcurrentBag<LinkDiagnostic>();
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = parallelism,
        };

        await Parallel.ForEachAsync(
            corpus.Pages,
            parallelOptions,
            (page, _) =>
            {
                ValidatePage(corpus, page, diagnostics);
                return ValueTask.CompletedTask;
            }).ConfigureAwait(false);

        return [.. diagnostics];
    }

    /// <summary>Validates one page's internal references against the corpus.</summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="page">Page to validate.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ValidatePage(ValidationCorpus corpus, PageLinks page, ConcurrentBag<LinkDiagnostic> sink)
    {
        for (var i = 0; i < page.InternalLinks.Length; i++)
        {
            var link = page.InternalLinks[i];
            if (string.IsNullOrEmpty(link))
            {
                continue;
            }

            ResolveAndReport(corpus, page, link, sink);
        }
    }

    /// <summary>Resolves one link against the corpus and reports any miss.</summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="source">Source page.</param>
    /// <param name="link">Raw href value.</param>
    /// <param name="sink">Diagnostic accumulator.</param>
    private static void ResolveAndReport(ValidationCorpus corpus, PageLinks source, string link, ConcurrentBag<LinkDiagnostic> sink)
    {
        var (target, fragment) = SplitTarget(link);

        // Pure same-page anchor: #id
        if (target.Length == 0)
        {
            if (fragment.Length > 0 && !source.AnchorIds.Contains(fragment))
            {
                sink.Add(new(source.PageUrl, link, LinkSeverity.Error, $"Same-page anchor '#{fragment}' has no matching heading id."));
            }

            return;
        }

        var resolved = Resolve(source.PageUrl, target);
        if (!corpus.TryGetPage(resolved, out var page))
        {
            sink.Add(new(source.PageUrl, link, LinkSeverity.Error, $"Internal link target '{resolved}' is not in the site."));
            return;
        }

        if (fragment.Length == 0 || page.AnchorIds.Contains(fragment))
        {
            return;
        }

        sink.Add(new(source.PageUrl, link, LinkSeverity.Error, $"Anchor '#{fragment}' on '{resolved}' has no matching heading id."));
    }

    /// <summary>Splits a raw link into (path, fragment).</summary>
    /// <param name="link">Raw href value.</param>
    /// <returns>Tuple of path-without-hash and fragment.</returns>
    private static (string Target, string Fragment) SplitTarget(string link)
    {
        var hash = link.IndexOf('#', StringComparison.Ordinal);
        return hash < 0
            ? (link, string.Empty)
            : (link[..hash], link[(hash + 1)..]);
    }

    /// <summary>Resolves <paramref name="target"/> relative to <paramref name="sourcePage"/>.</summary>
    /// <param name="sourcePage">Source page URL.</param>
    /// <param name="target">Target path (no fragment).</param>
    /// <returns>Forward-slashed site-relative URL.</returns>
    private static string Resolve(string sourcePage, string target)
    {
        if (target.StartsWith('/'))
        {
            return target.TrimStart('/');
        }

        var sourceDir = GetDirectory(sourcePage);
        var combined = string.IsNullOrEmpty(sourceDir) ? target : sourceDir + "/" + target;
        return Normalise(combined);
    }

    /// <summary>Returns the directory portion of <paramref name="pageUrl"/>, or empty when at the root.</summary>
    /// <param name="pageUrl">Page URL.</param>
    /// <returns>Directory prefix (no trailing slash).</returns>
    private static string GetDirectory(string pageUrl)
    {
        var slash = pageUrl.LastIndexOf('/');
        return slash < 0 ? string.Empty : pageUrl[..slash];
    }

    /// <summary>Collapses <c>./</c> and <c>../</c> segments.</summary>
    /// <param name="path">Forward-slashed path.</param>
    /// <returns>Normalised path.</returns>
    private static string Normalise(string path)
    {
        var segments = path.Split('/');
        var stack = new List<string>(segments.Length);
        for (var i = 0; i < segments.Length; i++)
        {
            var s = segments[i];
            if (s.Length == 0 || s == ".")
            {
                continue;
            }

            if (s == "..")
            {
                if (stack is [_, ..])
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                continue;
            }

            stack.Add(s);
        }

        return string.Join('/', stack);
    }
}
