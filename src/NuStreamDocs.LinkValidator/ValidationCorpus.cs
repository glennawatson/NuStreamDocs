// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Single in-memory store populated by one parallel sweep of the
/// emitted site. Both validators read from this; the disk tree is
/// never re-walked.
/// </summary>
/// <remarks>
/// Build via <see cref="BuildAsync"/> at <see cref="NuStreamDocs.Plugins.IDocPlugin.OnFinaliseAsync"/>
/// time. The result is an immutable snapshot: pages keyed by their
/// site-relative URL, every page exposing its links + anchors via a
/// <see cref="PageLinks"/> record.
/// </remarks>
public sealed class ValidationCorpus
{
    /// <summary>Lowercased file extensions treated as static assets rather than pages.</summary>
    private static readonly FrozenSet<string> AssetExtensions = FrozenSet.ToFrozenSet(
        [
            ".css", ".js", ".json", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp",
            ".ico", ".woff", ".woff2", ".ttf", ".map", ".xml", ".pdf", ".zip",
        ],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Pages keyed by site-relative URL (forward-slashed).</summary>
    private readonly FrozenDictionary<string, PageLinks> _pages;

    /// <summary>Initializes a new instance of the <see cref="ValidationCorpus"/> class.</summary>
    /// <param name="pages">Pages keyed by URL.</param>
    private ValidationCorpus(FrozenDictionary<string, PageLinks> pages) => _pages = pages;

    /// <summary>Gets every page in the corpus.</summary>
    public PageLinks[] Pages { get; private init; } = [];

    /// <summary>Walks <paramref name="outputRoot"/> in parallel and builds an immutable corpus.</summary>
    /// <param name="outputRoot">Absolute site output root.</param>
    /// <param name="parallelism">Maximum parallel readers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The populated corpus.</returns>
    public static async Task<ValidationCorpus> BuildAsync(string outputRoot, int parallelism, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallelism);

        var fullRoot = Path.GetFullPath(outputRoot);
        var pages = new ConcurrentDictionary<string, PageLinks>(StringComparer.Ordinal);

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = parallelism,
        };

        await Parallel.ForEachAsync(
            EnumerateHtml(fullRoot),
            parallelOptions,
            async (path, ct) =>
            {
                var bytes = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
                var pageUrl = ToPageUrl(fullRoot, path);
                pages[pageUrl] = ScanPage(pageUrl, bytes);
            }).ConfigureAwait(false);

        var frozen = pages.ToFrozenDictionary(StringComparer.Ordinal);
        return new(frozen) { Pages = [.. frozen.Values] };
    }

    /// <summary>Tests whether a page exists at <paramref name="pageUrl"/>.</summary>
    /// <param name="pageUrl">Site-relative URL.</param>
    /// <returns>True when the page is in the corpus.</returns>
    public bool ContainsPage(string pageUrl) =>
        _pages.ContainsKey(pageUrl);

    /// <summary>Resolves <paramref name="pageUrl"/> to its <see cref="PageLinks"/>.</summary>
    /// <param name="pageUrl">Site-relative URL.</param>
    /// <param name="page">Resolved page on success.</param>
    /// <returns>True when found.</returns>
    public bool TryGetPage(string pageUrl, out PageLinks page) =>
        _pages.TryGetValue(pageUrl, out page!);

    /// <summary>Yields every <c>.html</c> file under <paramref name="root"/>.</summary>
    /// <param name="root">Absolute site root.</param>
    /// <returns>Absolute paths.</returns>
    private static IEnumerable<string> EnumerateHtml(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories)
            : [];

    /// <summary>Converts an absolute <c>.html</c> path to its site-relative URL.</summary>
    /// <param name="root">Absolute site root.</param>
    /// <param name="absolutePath">Absolute path to the file.</param>
    /// <returns>Forward-slashed site-relative URL.</returns>
    private static string ToPageUrl(string root, string absolutePath) =>
        Path.GetRelativePath(root, absolutePath).Replace('\\', '/');

    /// <summary>Scans one page's HTML into a <see cref="PageLinks"/>.</summary>
    /// <param name="pageUrl">Page URL.</param>
    /// <param name="bytes">UTF-8 HTML.</param>
    /// <returns>The captured inventory.</returns>
    private static PageLinks ScanPage(string pageUrl, ReadOnlySpan<byte> bytes)
    {
        var hrefs = LinkExtractor.ExtractHrefs(bytes);
        var sources = LinkExtractor.ExtractSources(bytes);
        var ids = LinkExtractor.ExtractHeadingIds(bytes);

        var internalLinks = new List<string>(hrefs.Length);
        var externalLinks = new List<string>(4);
        for (var i = 0; i < hrefs.Length; i++)
        {
            var h = hrefs[i];
            if (IsExternal(h))
            {
                externalLinks.Add(h);
                continue;
            }

            if (IsAssetExtension(h))
            {
                continue;
            }

            internalLinks.Add(h);
        }

        for (var i = 0; i < sources.Length; i++)
        {
            var s = sources[i];
            if (IsExternal(s))
            {
                externalLinks.Add(s);
            }
        }

        return new(
            pageUrl,
            [.. internalLinks],
            [.. externalLinks],
            ids.ToFrozenSet(StringComparer.Ordinal));
    }

    /// <summary>True for absolute schemes that need network validation.</summary>
    /// <param name="url">URL string.</param>
    /// <returns>True for <c>http://</c>, <c>https://</c>.</returns>
    private static bool IsExternal(string url) =>
        url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>True for paths that look like static-asset references rather than pages.</summary>
    /// <param name="url">URL string (without query/fragment-stripping).</param>
    /// <returns>True for typical asset extensions.</returns>
    private static bool IsAssetExtension(string url)
    {
        // Strip fragment / query so the extension test sees the path.
        var path = url;
        var hash = path.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            path = path[..hash];
        }

        var query = path.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            path = path[..query];
        }

        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && AssetExtensions.Contains(ext);
    }
}
