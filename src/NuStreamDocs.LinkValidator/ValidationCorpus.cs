// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.LinkValidator;

/// <summary>In-memory inventory of every page in the rendered site, consumed by both validators.</summary>
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
        [.. ".pdf"u8], [.. ".zip"u8]
    ];

    /// <summary>Pages keyed by site-relative URL bytes (forward-slashed UTF-8).</summary>
    private readonly Dictionary<byte[], PageLinks> _pages;

    /// <summary>Static-asset paths on disk (non-HTML files) keyed by site-relative URL bytes.</summary>
    private readonly HashSet<byte[]> _assets;

    /// <summary>Initializes a new instance of the <see cref="ValidationCorpus"/> class.</summary>
    /// <param name="pages">Pages keyed by URL bytes.</param>
    /// <param name="assets">Static-asset URL set.</param>
    private ValidationCorpus(Dictionary<byte[], PageLinks> pages, HashSet<byte[]> assets)
    {
        _pages = pages;
        _assets = assets;
    }

    /// <summary>Gets every page in the corpus.</summary>
    public PageLinks[] Pages { get; private init; } = [];

    /// <summary>Builds an immutable corpus from a previously-collected page set (e.g. accumulated from per-page Scan hooks).</summary>
    /// <param name="pages">Pages keyed by URL bytes.</param>
    /// <returns>The populated corpus.</returns>
    public static ValidationCorpus FromPages(IDictionary<byte[], PageLinks> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        Dictionary<byte[], PageLinks> snapshot = new(pages, ByteArrayComparer.Instance);
        return new(snapshot, new(ByteArrayComparer.Instance)) { Pages = [.. snapshot.Values] };
    }

    /// <summary>Scans one page's HTML into a <see cref="PageLinks"/>.</summary>
    /// <param name="pageUrl">Page URL bytes.</param>
    /// <param name="html">UTF-8 HTML bytes.</param>
    /// <returns>The captured inventory.</returns>
    public static PageLinks Scan(byte[] pageUrl, ReadOnlySpan<byte> html)
    {
        ArgumentNullException.ThrowIfNull(pageUrl);
        return ScanPage(pageUrl, html);
    }

    /// <summary>Walks <paramref name="outputRoot"/> in parallel and builds an immutable corpus.</summary>
    /// <param name="outputRoot">Absolute site output root.</param>
    /// <param name="parallelism">Maximum parallel readers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The populated corpus.</returns>
    public static async Task<ValidationCorpus> BuildAsync(DirectoryPath outputRoot, int parallelism, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(outputRoot.Value);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parallelism);

        DirectoryPath fullRoot = new(Path.GetFullPath(outputRoot.Value));
        ConcurrentDictionary<byte[], PageLinks> pages = new(ByteArrayComparer.Instance);

        ParallelOptions parallelOptions = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = parallelism
        };

        await Parallel.ForEachAsync(
            EnumerateHtml(fullRoot),
            parallelOptions,
            async (path, ct) =>
            {
                var bytes = await File.ReadAllBytesAsync(path.Value, ct).ConfigureAwait(false);
                var pageUrlBytes = ToPageUrlBytes(fullRoot, path);

                // Redirect stubs (meta-refresh bouncers) are real pages users can land on —
                // register them so inbound links resolve, but skip their outbound link scan
                // since the bytes are just the redirect chrome.
                pages[pageUrlBytes] = IsRedirectStub(bytes)
                    ? new(pageUrlBytes, [], [], [], new(ByteArrayComparer.Instance), new(ByteArrayComparer.Instance))
                    : ScanPage(pageUrlBytes, bytes);
            }).ConfigureAwait(false);

        Dictionary<byte[], PageLinks> snapshot = new(pages, ByteArrayComparer.Instance);
        var assets = EnumerateAssetUrls(fullRoot);
        return new(snapshot, assets) { Pages = [.. snapshot.Values] };
    }

    /// <summary>True when an asset file exists at <paramref name="assetUrl"/>.</summary>
    /// <param name="assetUrl">Site-relative asset URL bytes (forward-slashed UTF-8, no leading slash).</param>
    /// <returns>True for known disk assets.</returns>
    public bool ContainsAsset(ReadOnlySpan<byte> assetUrl) =>
        _assets.GetAlternateLookup<ReadOnlySpan<byte>>().Contains(assetUrl);

    /// <summary>Tests whether a page exists at <paramref name="pageUrl"/>.</summary>
    /// <param name="pageUrl">Site-relative URL bytes.</param>
    /// <returns>True when the page is in the corpus.</returns>
    public bool ContainsPage(byte[] pageUrl)
    {
        ArgumentNullException.ThrowIfNull(pageUrl);
        return _pages.ContainsKey(pageUrl);
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

    /// <summary>Resolves <paramref name="pageUrl"/> to its <see cref="PageLinks"/>, accepting directory-URL variants (<c>foo/</c>, <c>foo</c>, empty path).</summary>
    /// <param name="pageUrl">Site-relative URL bytes.</param>
    /// <param name="page">Resolved page on success.</param>
    /// <returns>True when the URL or any directory-URL variant is in the corpus.</returns>
    public bool TryResolvePage(ReadOnlySpan<byte> pageUrl, out PageLinks page)
    {
        var lookup = _pages.GetAlternateLookup<ReadOnlySpan<byte>>();

        if (lookup.TryGetValue(pageUrl, out page!))
        {
            return true;
        }

        // Empty path → site root index.
        if (pageUrl.IsEmpty)
        {
            return lookup.TryGetValue("index.html"u8, out page!);
        }

        // foo/ → try foo/index.html (disk-style) then foo.html (source-style).
        if (pageUrl[^1] == (byte)'/')
        {
            Span<byte> withIndex = stackalloc byte[pageUrl.Length + "index.html"u8.Length];
            pageUrl.CopyTo(withIndex);
            "index.html"u8.CopyTo(withIndex[pageUrl.Length..]);
            if (lookup.TryGetValue(withIndex, out page!))
            {
                return true;
            }

            var trimmed = pageUrl[..^1];
            Span<byte> withHtmlNoSlash = stackalloc byte[trimmed.Length + ".html"u8.Length];
            trimmed.CopyTo(withHtmlNoSlash);
            ".html"u8.CopyTo(withHtmlNoSlash[trimmed.Length..]);
            return lookup.TryGetValue(withHtmlNoSlash, out page!);
        }

        // foo (no trailing slash, no .html) → try foo/index.html, then foo.html.
        if (!pageUrl.EndsWith(".html"u8))
        {
            Span<byte> withSlashIndex = stackalloc byte[pageUrl.Length + 1 + "index.html"u8.Length];
            pageUrl.CopyTo(withSlashIndex);
            withSlashIndex[pageUrl.Length] = (byte)'/';
            "index.html"u8.CopyTo(withSlashIndex[(pageUrl.Length + 1)..]);
            if (lookup.TryGetValue(withSlashIndex, out page!))
            {
                return true;
            }

            Span<byte> withHtml = stackalloc byte[pageUrl.Length + ".html"u8.Length];
            pageUrl.CopyTo(withHtml);
            ".html"u8.CopyTo(withHtml[pageUrl.Length..]);
            return lookup.TryGetValue(withHtml, out page!);
        }

        page = null!;
        return false;
    }

    /// <summary>Walks <paramref name="root"/> and snapshots every non-HTML file as a forward-slashed URL byte array.</summary>
    /// <param name="root">Absolute site output root.</param>
    /// <returns>Set of site-relative asset URLs.</returns>
    private static HashSet<byte[]> EnumerateAssetUrls(in DirectoryPath root)
    {
        HashSet<byte[]> set = new(ByteArrayComparer.Instance);
        if (!Directory.Exists(root.Value))
        {
            return set;
        }

        foreach (var path in Directory.EnumerateFiles(root.Value, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Pre-compressed siblings (.gz / .br) are emitted alongside the canonical asset
            // and don't need their own entries — author hrefs never point at them.
            if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".br", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rel = Path.GetRelativePath(root.Value, path).Replace('\\', '/');
            set.Add(Encoding.UTF8.GetBytes(rel));
        }

        return set;
    }

    /// <summary>Yields every <c>.html</c> file under <paramref name="root"/>.</summary>
    /// <param name="root">Absolute site root.</param>
    /// <returns>Absolute paths.</returns>
    private static IEnumerable<FilePath> EnumerateHtml(DirectoryPath root)
    {
        if (!Directory.Exists(root.Value))
        {
            yield break;
        }

        // foreach over IEnumerable<string> from Directory.EnumerateFiles — no indexed alternative.
        foreach (var path in Directory.EnumerateFiles(root.Value, "*.html", SearchOption.AllDirectories))
        {
            yield return new(path);
        }
    }

    /// <summary>Converts an absolute <c>.html</c> path to its site-relative URL bytes.</summary>
    /// <param name="root">Absolute site root.</param>
    /// <param name="absolutePath">Absolute path to the file.</param>
    /// <returns>Forward-slashed site-relative URL as UTF-8 bytes.</returns>
    private static byte[] ToPageUrlBytes(in DirectoryPath root, in FilePath absolutePath)
    {
        var relative = Path.GetRelativePath(root.Value, absolutePath.Value).Replace('\\', '/');
        return Encoding.UTF8.GetBytes(relative);
    }

    /// <summary>Scans one page's HTML into a <see cref="PageLinks"/>.</summary>
    /// <param name="pageUrl">Page URL bytes.</param>
    /// <param name="bytes">UTF-8 HTML.</param>
    /// <returns>The captured inventory.</returns>
    private static PageLinks ScanPage(byte[] pageUrl, ReadOnlySpan<byte> bytes)
    {
        var hrefs = LinkExtractor.ExtractHrefRanges(bytes);
        var srcs = LinkExtractor.ExtractSrcRanges(bytes);
        var idRanges = LinkExtractor.ExtractIdRanges(bytes);
        var nameRanges = LinkExtractor.ExtractDeprecatedNameAnchorRanges(bytes);

        List<byte[]> internalLinks = new(hrefs.Length);
        List<byte[]> externalLinks = new(4);
        List<byte[]> internalAssets = new(srcs.Length);
        BucketHrefs(hrefs, bytes, internalLinks, externalLinks, internalAssets);
        BucketSrcs(srcs, bytes, externalLinks, internalAssets);

        HashSet<byte[]> anchorIds = new(idRanges.Length, ByteArrayComparer.Instance);
        for (var i = 0; i < idRanges.Length; i++)
        {
            anchorIds.Add([.. idRanges[i].AsSpan(bytes)]);
        }

        var deprecatedNameAnchors = nameRanges.Length is 0
            ? EmptyCollections.HashSetFor<byte[]>()
            : BuildDeprecatedNameSet(nameRanges, bytes);

        return new(
            pageUrl,
            [.. internalLinks],
            [.. externalLinks],
            [.. internalAssets],
            anchorIds,
            deprecatedNameAnchors);
    }

    /// <summary>Buckets <c>href</c> ranges into internal-link / external / internal-asset lists.</summary>
    /// <param name="hrefs">Range list returned by <see cref="LinkExtractor.ExtractHrefRanges"/>.</param>
    /// <param name="bytes">Page HTML bytes the ranges index into.</param>
    /// <param name="internalLinks">Sink for relative page hrefs.</param>
    /// <param name="externalLinks">Sink for absolute http(s) hrefs.</param>
    /// <param name="internalAssets">Sink for asset-shaped local hrefs (e.g. <c>report.pdf</c>, <c>extra.css</c>).</param>
    private static void BucketHrefs(
        ByteRange[] hrefs,
        ReadOnlySpan<byte> bytes,
        List<byte[]> internalLinks,
        List<byte[]> externalLinks,
        List<byte[]> internalAssets)
    {
        for (var i = 0; i < hrefs.Length; i++)
        {
            var slice = hrefs[i].AsSpan(bytes);
            if (IsNonValidatableScheme(slice))
            {
                continue;
            }

            if (IsExternal(slice))
            {
                externalLinks.Add([.. slice]);
                continue;
            }

            // Asset-shaped local hrefs (e.g. `<a href="report.pdf">`, `<link href="extra.css">`)
            // get bucketed for the asset validator instead of the page validator. They'd never
            // resolve in the page corpus so previously they were silently dropped — now we
            // confirm the file is actually on disk.
            if (IsAssetExtension(slice))
            {
                internalAssets.Add([.. slice]);
                continue;
            }

            internalLinks.Add([.. slice]);
        }
    }

    /// <summary>Buckets <c>src</c> ranges into external / internal-asset lists.</summary>
    /// <param name="srcs">Range list returned by <see cref="LinkExtractor.ExtractSrcRanges"/>.</param>
    /// <param name="bytes">Page HTML bytes the ranges index into.</param>
    /// <param name="externalLinks">Sink for absolute http(s) and protocol-relative srcs.</param>
    /// <param name="internalAssets">Sink for relative srcs pointing at on-disk files.</param>
    private static void BucketSrcs(
        ByteRange[] srcs,
        ReadOnlySpan<byte> bytes,
        List<byte[]> externalLinks,
        List<byte[]> internalAssets)
    {
        for (var i = 0; i < srcs.Length; i++)
        {
            var slice = srcs[i].AsSpan(bytes);
            if (IsNonValidatableScheme(slice))
            {
                continue;
            }

            // Protocol-relative srcs (`//host/path`) inherit the page's scheme and resolve
            // externally; same bucket as absolute http(s).
            if (IsExternal(slice) || IsProtocolRelative(slice))
            {
                externalLinks.Add([.. slice]);
                continue;
            }

            internalAssets.Add([.. slice]);
        }
    }

    /// <summary>Materializes the obsolete <c>&lt;a name=&quot;&quot;&gt;</c> anchor values into a byte-array-keyed set.</summary>
    /// <param name="ranges">Extracted name-attribute ranges.</param>
    /// <param name="bytes">Page HTML bytes.</param>
    /// <returns>Hash set keyed by the byte values of each name attribute.</returns>
    private static HashSet<byte[]> BuildDeprecatedNameSet(ByteRange[] ranges, ReadOnlySpan<byte> bytes)
    {
        HashSet<byte[]> set = new(ranges.Length, ByteArrayComparer.Instance);
        for (var i = 0; i < ranges.Length; i++)
        {
            set.Add([.. ranges[i].AsSpan(bytes)]);
        }

        return set;
    }

    /// <summary>True when <paramref name="html"/> is a meta-refresh redirect stub (no real content to validate).</summary>
    /// <param name="html">UTF-8 page bytes.</param>
    /// <returns>True for stubs emitted by the redirects plugin or hand-authored equivalents.</returns>
    private static bool IsRedirectStub(ReadOnlySpan<byte> html) =>
        html.IndexOf("http-equiv=\"refresh\""u8) >= 0;

    /// <summary>True for absolute schemes that need network validation.</summary>
    /// <param name="url">URL bytes.</param>
    /// <returns>True for <c>http://</c> or <c>https://</c> (case-insensitive).</returns>
    private static bool IsExternal(ReadOnlySpan<byte> url) =>
        AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "http://"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "https://"u8);

    /// <summary>True for protocol-relative URLs (<c>//host/path</c>) — these inherit the page's scheme and resolve externally.</summary>
    /// <param name="url">URL bytes.</param>
    /// <returns>True when the value starts with <c>//</c>.</returns>
    private static bool IsProtocolRelative(ReadOnlySpan<byte> url) =>
        url is [(byte)'/', (byte)'/', ..];

    /// <summary>True for schemes the validator can neither resolve internally nor probe over HTTP.</summary>
    /// <param name="url">URL bytes.</param>
    /// <returns>True for <c>mailto:</c>, <c>tel:</c>, <c>sms:</c>, <c>javascript:</c>, or <c>data:</c>.</returns>
    private static bool IsNonValidatableScheme(ReadOnlySpan<byte> url) =>
        AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "mailto:"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "tel:"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "sms:"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "javascript:"u8)
        || AsciiByteHelpers.StartsWithIgnoreAsciiCase(url, 0, "data:"u8);

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
