// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace NuStreamDocs.LinkValidator.Tests;

/// <summary>Branch-coverage tests for the ValidationCorpus loader.</summary>
public class ValidationCorpusTests
{
    /// <summary>External links (http/https) and asset extensions are filtered out of the internal-links list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExternalsAndAssetsAreFilteredFromInternalLinks()
    {
        var dir = TempDir();
        try
        {
            const string Html = "<a href=\"https://example.com\">ext1</a>" +
                "<a href=\"http://x.test\">ext2</a>" +
                "<a href=\"about.html\">internal</a>" +
                "<a href=\"image.png\">asset</a>" +
                "<a href=\"style.css\">asset2</a>" +
                "<img src=\"https://cdn.test/x.jpg\" />";
            await File.WriteAllTextAsync(Path.Combine(dir, "index.html"), Html);

            var corpus = await ValidationCorpus.BuildAsync(dir, parallelism: 2, CancellationToken.None);
            await Assert.That(corpus.TryGetPage("index.html", out var page)).IsTrue();
            await Assert.That(ContainsBytes(page.InternalLinks, "about.html"u8)).IsTrue();
            await Assert.That(ContainsBytes(page.InternalLinks, "https://example.com"u8)).IsFalse();
            await Assert.That(ContainsBytes(page.InternalLinks, "image.png"u8)).IsFalse();
            await Assert.That(ContainsBytes(page.ExternalLinks, "https://example.com"u8)).IsTrue();
            await Assert.That(ContainsBytes(page.ExternalLinks, "http://x.test"u8)).IsTrue();
            await Assert.That(ContainsBytes(page.ExternalLinks, "https://cdn.test/x.jpg"u8)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Asset detection ignores fragments and query strings before checking the extension.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AssetDetectionStripsFragmentAndQuery()
    {
        var dir = TempDir();
        try
        {
            const string Html = "<a href=\"image.png?v=1\">asset</a>" +
                "<a href=\"style.css#section\">asset</a>" +
                "<a href=\"page.html?q=1\">page</a>";
            await File.WriteAllTextAsync(Path.Combine(dir, "index.html"), Html);

            var corpus = await ValidationCorpus.BuildAsync(dir, parallelism: 2, CancellationToken.None);
            await Assert.That(corpus.TryGetPage("index.html", out var page)).IsTrue();
            await Assert.That(ContainsBytes(page.InternalLinks, "image.png?v=1"u8)).IsFalse();
            await Assert.That(ContainsBytes(page.InternalLinks, "style.css#section"u8)).IsFalse();
            await Assert.That(ContainsBytes(page.InternalLinks, "page.html?q=1"u8)).IsTrue();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>An entirely missing output root yields a corpus with no pages.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingRootYieldsEmptyCorpus()
    {
        var corpus = await ValidationCorpus.BuildAsync(
            "/does-not-exist-" + Guid.NewGuid().ToString("N"),
            parallelism: 1,
            CancellationToken.None);
        await Assert.That(corpus.Pages.Length).IsEqualTo(0);
    }

    /// <summary>ContainsPage returns false for an unknown URL and true for a registered one.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ContainsPageReflectsCorpus()
    {
        var dir = TempDir();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "index.html"), "<h1 id=\"a\">Hi</h1>");
            var corpus = await ValidationCorpus.BuildAsync(dir, parallelism: 1, CancellationToken.None);
            await Assert.That(corpus.ContainsPage("index.html")).IsTrue();
            await Assert.That(corpus.ContainsPage("index.html"u8.ToArray())).IsTrue();
            await Assert.That(corpus.ContainsPage("missing.html")).IsFalse();
            await Assert.That(corpus.ContainsPage("missing.html"u8.ToArray())).IsFalse();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>BuildAsync rejects a zero-or-negative parallelism.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildAsyncRejectsZeroParallelism() =>
        await Assert.That(async () => _ = await ValidationCorpus.BuildAsync("/tmp", 0, CancellationToken.None))
            .Throws<ArgumentOutOfRangeException>();

    /// <summary>Disposable scratch directory.</summary>
    /// <returns>Absolute path.</returns>
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smd-vc-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Tests whether <paramref name="haystack"/> holds a byte array equal to <paramref name="needle"/>.</summary>
    /// <param name="haystack">Byte array list.</param>
    /// <param name="needle">UTF-8 bytes to search for.</param>
    /// <returns>True when found.</returns>
    private static bool ContainsBytes(byte[][] haystack, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i < haystack.Length; i++)
        {
            if (haystack[i].AsSpan().SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
