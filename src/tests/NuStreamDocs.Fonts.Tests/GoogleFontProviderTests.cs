// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Fonts.Tests;

/// <summary>Coverage for <see cref="GoogleFontProvider"/> (offline, fixture-fed).</summary>
public class GoogleFontProviderTests
{
    /// <summary>A captured css2 fixture with a labelled <c>latin</c> block and a labelled <c>cyrillic</c> block.</summary>
    private const string GoogleCss = """
                                     /* cyrillic */
                                     @font-face {
                                       font-family: 'Source Sans 3';
                                       font-style: normal;
                                       font-weight: 400;
                                       src: url(https://fonts.gstatic.com/s/sourcesans3/v18/cyr.woff2) format('woff2');
                                       unicode-range: U+0400-045F;
                                     }
                                     /* latin */
                                     @font-face {
                                       font-family: 'Source Sans 3';
                                       font-style: normal;
                                       font-weight: 400;
                                       src: url(https://fonts.gstatic.com/s/sourcesans3/v18/lat.woff2) format('woff2');
                                       unicode-range: U+0000-00FF;
                                     }
                                     """;

    /// <summary>The css2 URL encodes the family with <c>+</c>, the (sorted) weight list, and the display token — and carries no <c>subset=</c> param (css2 ignores it).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildsCss2Url()
    {
        var face = FontsOptions.Default.AddGoogleFont("Source Sans 3"u8, 700, 400).Faces[0];
        var url = (string)GoogleFontProvider.BuildStylesheetUrl(face);
        await Assert.That(url)
            .IsEqualTo("https://fonts.googleapis.com/css2?family=Source+Sans+3:wght@400;700&display=swap");
        await Assert.That(url).DoesNotContain("subset=");
    }

    /// <summary>Resolving keeps only the requested subset's blocks (css2 returns all of them) and downloads just those woff2 files.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ResolvesAndFiltersBySubset()
    {
        using TempDir dir = new();
        var cache = new FontDownloadCache(dir.Root, true);
        var face = FontsOptions.Default.AddGoogleFont("Source Sans 3"u8, 400).Faces[0];

        await File.WriteAllBytesAsync(
            cache.CacheFilePath(GoogleFontProvider.BuildStylesheetUrl(face)),
            Encoding.UTF8.GetBytes(GoogleCss));
        byte[] latinBytes = [10, 20, 30];
        await File.WriteAllBytesAsync(
            cache.CacheFilePath("https://fonts.gstatic.com/s/sourcesans3/v18/lat.woff2"),
            latinBytes);

        // Request only "latin": the cyrillic block (and its woff2, which isn't even in the cache) must be dropped.
        var resources = await GoogleFontProvider.Instance.ResolveAsync(
            face,
            [[.. "latin"u8]],
            cache,
            default,
            null,
            CancellationToken.None);
        await Assert.That(resources.Length).IsEqualTo(1);
        await Assert.That(resources[0].Weight).IsEqualTo(400);
        await Assert.That(resources[0].Woff2Bytes.SequenceEqual(latinBytes)).IsTrue();
        await Assert.That(Encoding.UTF8.GetString(resources[0].FamilyBytes)).IsEqualTo("Source Sans 3");
    }

    /// <summary>With a usage bitset (the <c>auto</c> path), a subset covering nothing the site uses is dropped <em>before</em> its woff2 is fetched.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AutoUsageFilterSkipsUnusedSubsetWithoutDownloading()
    {
        using TempDir dir = new();
        var cache = new FontDownloadCache(dir.Root, true);
        var face = FontsOptions.Default.AddGoogleFont("Source Sans 3"u8, 400).Faces[0];

        await File.WriteAllBytesAsync(
            cache.CacheFilePath(GoogleFontProvider.BuildStylesheetUrl(face)),
            Encoding.UTF8.GetBytes(GoogleCss));
        byte[] latinBytes = [11, 22, 33];

        // Only the latin woff2 is in the cache. If the resolver tried to fetch cyr.woff2, it would throw (offline miss).
        await File.WriteAllBytesAsync(
            cache.CacheFilePath("https://fonts.gstatic.com/s/sourcesans3/v18/lat.woff2"),
            latinBytes);

        var asciiOnly = UnicodeRangeMatcher.NewSeenBlocks();
        var resources = await GoogleFontProvider.Instance.ResolveAsync(
            face,
            [[.. "all"u8]],
            cache,
            default,
            asciiOnly,
            CancellationToken.None);
        await Assert.That(resources.Length).IsEqualTo(1);
        await Assert.That(resources[0].Woff2Bytes.SequenceEqual(latinBytes)).IsTrue();
    }
}
