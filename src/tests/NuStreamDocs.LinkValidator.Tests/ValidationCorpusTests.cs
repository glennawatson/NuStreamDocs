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
            await Assert.That(corpus.ContainsPage([.. "index.html"u8])).IsTrue();
            await Assert.That(corpus.ContainsPage("missing.html")).IsFalse();
            await Assert.That(corpus.ContainsPage([.. "missing.html"u8])).IsFalse();
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
        await Assert.That(static async () => _ = await ValidationCorpus.BuildAsync("/tmp", 0, CancellationToken.None))
            .Throws<ArgumentOutOfRangeException>();

    /// <summary>TryResolvePage matches the verbatim URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryResolvePageMatchesVerbatim()
    {
        var corpus = BuildCorpus([.. "foo.html"u8]);
        await Assert.That(corpus.TryResolvePage("foo.html"u8, out _)).IsTrue();
    }

    /// <summary>TryResolvePage maps directory-style URLs to the on-disk index file.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryResolvePageHandlesTrailingSlashToIndex()
    {
        var corpus = BuildCorpus([.. "foo/index.html"u8]);
        await Assert.That(corpus.TryResolvePage("foo/"u8, out _)).IsTrue();
    }

    /// <summary>TryResolvePage falls back to the source-page form when the corpus is keyed on the .html URL.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryResolvePageHandlesTrailingSlashToHtml()
    {
        var corpus = BuildCorpus([.. "foo.html"u8]);
        await Assert.That(corpus.TryResolvePage("foo/"u8, out _)).IsTrue();
    }

    /// <summary>TryResolvePage with an empty path resolves to the root index.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryResolvePageEmptyPathResolvesRootIndex()
    {
        var corpus = BuildCorpus([.. "index.html"u8]);
        await Assert.That(corpus.TryResolvePage(default, out _)).IsTrue();
    }

    /// <summary>TryResolvePage with a bare name resolves to {name}/index.html.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryResolvePageBareNameToDirIndex()
    {
        var corpus = BuildCorpus([.. "Splat/index.html"u8]);
        await Assert.That(corpus.TryResolvePage("Splat"u8, out _)).IsTrue();
    }

    /// <summary>TryResolvePage with a bare name falls back to {name}.html.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryResolvePageBareNameToHtml()
    {
        var corpus = BuildCorpus([.. "Splat.html"u8]);
        await Assert.That(corpus.TryResolvePage("Splat"u8, out _)).IsTrue();
    }

    /// <summary>TryResolvePage misses cleanly when no variant matches.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryResolvePageMissReturnsFalse()
    {
        var corpus = BuildCorpus([.. "known.html"u8]);
        await Assert.That(corpus.TryResolvePage("does-not-exist/"u8, out _)).IsFalse();
    }

    /// <summary>Builds an in-memory corpus seeded with one URL whose body is an empty HTML span.</summary>
    /// <param name="url">The corpus URL.</param>
    /// <returns>The populated corpus.</returns>
    private static ValidationCorpus BuildCorpus(byte[] url)
    {
        var pages = new Dictionary<byte[], PageLinks>(Common.ByteArrayComparer.Instance)
        {
            [url] = ValidationCorpus.Scan(url, "<p>x</p>"u8),
        };
        return ValidationCorpus.FromPages(pages);
    }

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
