// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="FontDownloadCache"/> (no live network).</summary>
public class FontDownloadCacheTests
{
    /// <summary>A pre-populated cache file is returned without any network access.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsCachedBytes()
    {
        using TempDir dir = new();
        var cache = new FontDownloadCache(dir.Root, true);
        byte[] payload = [1, 2, 3, 4, 5];
        await File.WriteAllBytesAsync(cache.CacheFilePath("https://example.test/font.woff2"), payload);
        var got = await cache.GetAsync("https://example.test/font.woff2", CancellationToken.None);
        await Assert.That(got.SequenceEqual(payload)).IsTrue();
    }

    /// <summary>An offline-mode miss throws <see cref="FontDownloadException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OfflineMissThrows()
    {
        using TempDir dir = new();
        var cache = new FontDownloadCache(dir.Root, true);
        var threw = false;
        try
        {
            _ = await cache.GetAsync("https://example.test/missing.woff2", CancellationToken.None);
        }
        catch (FontDownloadException)
        {
            threw = true;
        }

        await Assert.That(threw).IsTrue();
    }
}
