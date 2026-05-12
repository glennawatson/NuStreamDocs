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
        var cached = await TryReadCachedAsync(path, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
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

        await PersistAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    /// <summary>Returns the cache file path for <paramref name="url"/>.</summary>
    /// <param name="url">Absolute URL.</param>
    /// <returns>The absolute cache file path.</returns>
    internal string CacheFilePath(ApiCompatString url) =>
        Path.Combine(_cacheDirectory.Value, HashFileName(url.Value ?? string.Empty));

    /// <summary>Reads a cached entry, tolerating a concurrent writer (returns null so the caller re-fetches).</summary>
    /// <param name="path">Cache file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached bytes, or null on a miss or a transient read failure.</returns>
    private static async Task<byte[]?> TryReadCachedAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Another build is mid-write to this content-addressed entry — fall back to re-fetching.
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Same, surfaced as an access denial on Windows when the entry is being replaced.
            return null;
        }
    }

    /// <summary>Deletes <paramref name="path"/> if it exists, ignoring I/O errors.</summary>
    /// <param name="path">File to delete.</param>
    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temporary file.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of a temporary file.
        }
    }

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

    /// <summary>
    /// Writes <paramref name="bytes"/> to the cache via a unique temp file then a rename. The entry
    /// is content-addressed, so an existing target already holds identical bytes — we never overwrite
    /// it; a rename onto an existing (or open) target simply fails and is treated as "lost the race".
    /// </summary>
    /// <param name="path">Cache file path.</param>
    /// <param name="bytes">Resource bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the entry is persisted (or the attempt was abandoned).</returns>
    private async Task PersistAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            // A concurrent build already populated this content-addressed entry.
            return;
        }

        Directory.CreateDirectory(_cacheDirectory.Value);
        var temp = StringCompose.Concat(path, ".", Guid.NewGuid().ToString("N"));
        try
        {
            await File.WriteAllBytesAsync(temp, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temp, path);
        }
        catch (IOException)
        {
            // Lost a race with another build writing the same bytes; the cache entry is still valid.
            DeleteQuietly(temp);
        }
        catch (UnauthorizedAccessException)
        {
            // Windows surfaces the same race as an access denial when the target is open; same handling.
            DeleteQuietly(temp);
        }
    }
}
