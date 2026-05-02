// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.LinkValidator;

/// <summary>
/// Single in-memory store populated by one parallel sweep of the
/// emitted site. Both validators read from this; the disk tree is
/// never re-walked.
/// </summary>
/// <remarks>
/// Built via <see cref="BuildAsync"/> at finalize time. All keys and
/// values are raw UTF-8 byte arrays; the corpus is keyed on a
/// byte-array <see cref="Dictionary{TKey, TValue}"/> with
/// <see cref="ByteArrayComparer"/> so the validator's per-link
/// resolution does no UTF-16 transcoding.
/// </remarks>
public sealed class ValidationCorpus
{
    /// <summary>Lowercased asset extensions (with leading dot) treated as static assets rather than pages.</summary>
    private static readonly byte[][] AssetExtensions =
    [
        [.. ".css"u8], [.. ".js"u8], [.. ".json"u8],
        [.. ".png"u8], [.. ".jpg"u8], [.. ".jpeg"u8],
        [.. ".gif"u8], [.. ".svg"u8], [.. ".webp"u8],
        [.. ".ico"u8], [.. ".woff"u8], [.. ".woff2"u8],
        [.. ".ttf"u8], [.. ".map"u8], [.. ".xml"u8],
        [.. ".pdf"u8], [.. ".zip"u8],
    ];

    /// <summary>Pages keyed by site-relative URL bytes (forward-slashed UTF-8).</summary>
    private readonly Dictionary<byte[], PageLinks> _pages;

    /// <summary>Initializes a new instance of the <see cref="ValidationCorpus"/> class.</summary>
    /// <param name="pages">Pages keyed by URL bytes.</param>
    private ValidationCorpus(Dictionary<byte[], PageLinks> pages) => _pages = pages;

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
        var pages = new ConcurrentDictionary<byte[], PageLinks>(ByteArrayComparer.Instance);

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
                var pageUrlBytes = ToPageUrlBytes(fullRoot, path);
                pages[pageUrlBytes] = ScanPage(pageUrlBytes, bytes);
            }).ConfigureAwait(false);

        var snapshot = new Dictionary<byte[], PageLinks>(pages, ByteArrayComparer.Instance);
        return new(snapshot) { Pages = [.. snapshot.Values] };
    }

    /// <summary>Tests whether a page exists at <paramref name="pageUrl"/>.</summary>
    /// <param name="pageUrl">Site-relative URL bytes.</param>
    /// <returns>True when the page is in the corpus.</returns>
    public bool ContainsPage(byte[] pageUrl)
    {
        ArgumentNullException.ThrowIfNull(pageUrl);
        return _pages.ContainsKey(pageUrl);
    }

    /// <summary>Tests whether a page exists at <paramref name="pageUrl"/> via the string-shaped diagnostic boundary.</summary>
    /// <param name="pageUrl">Site-relative URL string.</param>
    /// <returns>True when the page is in the corpus.</returns>
    /// <remarks>String adapter retained for tests and diagnostic-layer callers; performance-critical paths use the byte overload.</remarks>
    public bool ContainsPage(string pageUrl)
    {
        ArgumentException.ThrowIfNullOrEmpty(pageUrl);
        return _pages.ContainsKey(Encoding.UTF8.GetBytes(pageUrl));
    }

    /// <summary>Resolves <paramref name="pageUrl"/> to its <see cref="PageLinks"/>.</summary>
    /// <param name="pageUrl">Site-relative URL bytes.</param>
    /// <param name="page">Resolved page on success.</param>
    /// <returns>True when found.</returns>
    public bool TryGetPage(byte[] pageUrl, out PageLinks page)
    {
        ArgumentNullException.ThrowIfNull(pageUrl);
        return _pages.TryGetValue(pageUrl, out page!);
    }

    /// <summary>Resolves <paramref name="pageUrl"/> to its <see cref="PageLinks"/> via the string-shaped diagnostic boundary.</summary>
    /// <param name="pageUrl">Site-relative URL string.</param>
    /// <param name="page">Resolved page on success.</param>
    /// <returns>True when found.</returns>
    public bool TryGetPage(string pageUrl, out PageLinks page)
    {
        ArgumentException.ThrowIfNullOrEmpty(pageUrl);
        return _pages.TryGetValue(Encoding.UTF8.GetBytes(pageUrl), out page!);
    }

    /// <summary>Yields every <c>.html</c> file under <paramref name="root"/>.</summary>
    /// <param name="root">Absolute site root.</param>
    /// <returns>Absolute paths.</returns>
    private static IEnumerable<string> EnumerateHtml(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.html", SearchOption.AllDirectories)
            : [];

    /// <summary>Converts an absolute <c>.html</c> path to its site-relative URL bytes.</summary>
    /// <param name="root">Absolute site root.</param>
    /// <param name="absolutePath">Absolute path to the file.</param>
    /// <returns>Forward-slashed site-relative URL as UTF-8 bytes.</returns>
    private static byte[] ToPageUrlBytes(string root, string absolutePath)
    {
        var relative = Path.GetRelativePath(root, absolutePath).Replace('\\', '/');
        return Encoding.UTF8.GetBytes(relative);
    }

    /// <summary>Scans one page's HTML into a <see cref="PageLinks"/>, classifying values in bytes before allocating any byte array.</summary>
    /// <param name="pageUrl">Page URL bytes.</param>
    /// <param name="bytes">UTF-8 HTML.</param>
    /// <returns>The captured inventory.</returns>
    private static PageLinks ScanPage(byte[] pageUrl, ReadOnlySpan<byte> bytes)
    {
        var hrefs = LinkExtractor.ExtractHrefRanges(bytes);
        var srcs = LinkExtractor.ExtractSrcRanges(bytes);
        var idRanges = LinkExtractor.ExtractHeadingIdRanges(bytes);

        var internalLinks = new List<byte[]>(hrefs.Length);
        var externalLinks = new List<byte[]>(4);
        for (var i = 0; i < hrefs.Length; i++)
        {
            var slice = hrefs[i].AsSpan(bytes);
            if (IsExternal(slice))
            {
                externalLinks.Add(slice.ToArray());
                continue;
            }

            if (IsAssetExtension(slice))
            {
                continue;
            }

            internalLinks.Add(slice.ToArray());
        }

        for (var i = 0; i < srcs.Length; i++)
        {
            var slice = srcs[i].AsSpan(bytes);
            if (IsExternal(slice))
            {
                externalLinks.Add(slice.ToArray());
            }
        }

        var anchorIds = new HashSet<byte[]>(idRanges.Length, ByteArrayComparer.Instance);
        for (var i = 0; i < idRanges.Length; i++)
        {
            anchorIds.Add(idRanges[i].AsSpan(bytes).ToArray());
        }

        return new(
            pageUrl,
            [.. internalLinks],
            [.. externalLinks],
            anchorIds);
    }

    /// <summary>True for absolute schemes that need network validation.</summary>
    /// <param name="url">URL bytes.</param>
    /// <returns>True for <c>http://</c> or <c>https://</c> (case-insensitive).</returns>
    private static bool IsExternal(ReadOnlySpan<byte> url) =>
        AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "http://"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "https://"u8);

    /// <summary>True for paths that look like static-asset references rather than pages.</summary>
    /// <param name="url">URL bytes (with optional fragment / query suffix).</param>
    /// <returns>True for typical asset extensions.</returns>
    private static bool IsAssetExtension(ReadOnlySpan<byte> url)
    {
        var path = url;

        // Strip fragment / query so the extension test sees the path.
        var hash = path.IndexOf((byte)'#');
        if (hash >= 0)
        {
            path = path[..hash];
        }

        var query = path.IndexOf((byte)'?');
        if (query >= 0)
        {
            path = path[..query];
        }

        var dot = path.LastIndexOf((byte)'.');
        if (dot < 0)
        {
            return false;
        }

        var ext = path[dot..];
        for (var i = 0; i < AssetExtensions.Length; i++)
        {
            if (AsciiByteHelpers.EqualsIgnoreAsciiCase(ext, AssetExtensions[i]))
            {
                return true;
            }
        }

        return false;
    }
}
