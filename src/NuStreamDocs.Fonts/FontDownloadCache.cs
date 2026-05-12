// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>A content-addressed on-disk cache for downloaded font files and stylesheets, so a second build (or warm CI) needs no network.</summary>
public sealed class FontDownloadCache
{
    /// <summary>Number of hash bytes used in a cache filename (16 bytes → 32 hex chars).</summary>
    private const int FilenameHashBytes = 16;

    /// <summary>Shared HTTP client; a desktop User-Agent makes the Google <c>css2</c> API return woff2.</summary>
    private static readonly HttpClient Client = CreateClient();

    /// <summary>Cache root directory.</summary>
    private readonly DirectoryPath _cacheDirectory;

    /// <summary>When true, a cache miss is an error instead of a download.</summary>
    private readonly bool _offline;

    /// <summary>Initializes a new instance of the <see cref="FontDownloadCache"/> class.</summary>
    /// <param name="cacheDirectory">Cache root; when empty, a directory under the temp path is used.</param>
    /// <param name="offline">When true, a cache miss throws instead of downloading.</param>
    public FontDownloadCache(in DirectoryPath cacheDirectory, bool offline)
    {
        _cacheDirectory = cacheDirectory.IsEmpty ? Path.Combine(Path.GetTempPath(), "nustreamdocs-fonts-cache") : cacheDirectory;
        _offline = offline;
    }

    /// <summary>Returns the bytes at <paramref name="url"/>, fetching and caching them on a miss.</summary>
    /// <param name="url">Absolute URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resource bytes.</returns>
    /// <exception cref="FontDownloadException">On an offline-mode miss or a network/HTTP failure.</exception>
    public async Task<byte[]> GetAsync(ApiCompatString url, CancellationToken cancellationToken)
    {
        var path = CacheFilePath(url);
        if (File.Exists(path))
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }

        if (_offline)
        {
            throw new FontDownloadException(StringCompose.Concat("Font resource not in the cache and offline mode is enabled: ", url, " — run once online to populate the cache."));
        }

        byte[] bytes;
        try
        {
            using var response = await Client.GetAsync(new Uri(url.Value ?? string.Empty, UriKind.Absolute), cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new FontDownloadException(StringCompose.Concat("Failed to download font resource: ", url), ex);
        }

        Directory.CreateDirectory(_cacheDirectory.Value);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    /// <summary>Returns the cache file path for <paramref name="url"/>.</summary>
    /// <param name="url">Absolute URL.</param>
    /// <returns>The absolute cache file path.</returns>
    internal string CacheFilePath(ApiCompatString url) =>
        Path.Combine(_cacheDirectory.Value, HashFileName(url.Value ?? string.Empty));

    /// <summary>Builds an HTTP client with a desktop User-Agent.</summary>
    /// <returns>The configured client.</returns>
    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        return client;
    }

    /// <summary>Hashes a URL into a hex filename.</summary>
    /// <param name="url">The URL.</param>
    /// <returns>A <c>&lt;hex&gt;.bin</c> filename.</returns>
    private static string HashFileName(string url)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return StringCompose.Concat(Convert.ToHexStringLower(hash.AsSpan(0, FilenameHashBytes)), ".bin");
    }
}
