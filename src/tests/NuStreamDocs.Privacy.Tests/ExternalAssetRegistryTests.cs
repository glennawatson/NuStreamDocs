// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Branch-coverage tests for ExternalAssetRegistry.</summary>
public class ExternalAssetRegistryTests
{
    /// <summary>Recognized file extensions are preserved on the local path.</summary>
    /// <param name="url">Source URL.</param>
    /// <param name="expectedExt">Expected suffix on the registered path.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("https://x.test/a.png", ".png")]
    [Arguments("https://x.test/a/b.css", ".css")]
    [Arguments("https://x.test/a.WOFF2", ".WOFF2")]
    [Arguments("https://x.test/a.svg?v=1", ".svg")]
    public async Task RecognizedExtensionPreserved(string url, string expectedExt)
    {
        var registry = new ExternalAssetRegistry("assets/external");
        var local = registry.GetOrAdd(url);
        await Assert.That(local).EndsWith(expectedExt);
        await Assert.That(local).StartsWith("assets/external/");
    }

    /// <summary>Paths without a recognized extension drop to a hash-only filename.</summary>
    /// <param name="url">Source URL.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("https://x.test/no-ext")]
    [Arguments("https://x.test/path.with/slashes")]
    [Arguments("https://x.test/.")]
    [Arguments("https://x.test/file.veryverylong")]
    [Arguments("not-a-uri")]
    public async Task UnusableExtensionStripped(string url)
    {
        var registry = new ExternalAssetRegistry("assets/external");
        var local = registry.GetOrAdd(url);
        await Assert.That(local).StartsWith("assets/external/");
        await Assert.That(local.Contains('.', StringComparison.Ordinal) && local.LastIndexOf('.') > "assets/external/".Length - 1).IsFalse();
    }

    /// <summary>The same URL maps to the same local path on subsequent calls.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GetOrAddIsIdempotent()
    {
        var registry = new ExternalAssetRegistry("d");
        var a = registry.GetOrAdd("https://x.test/a.png");
        var b = registry.GetOrAdd("https://x.test/a.png");
        await Assert.That(a).IsEqualTo(b);
    }

    /// <summary>Different URLs map to different local paths (hash-driven).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DifferentUrlsDistinctPaths()
    {
        var registry = new ExternalAssetRegistry("d");
        var a = registry.GetOrAdd("https://x.test/a.png");
        var b = registry.GetOrAdd("https://x.test/b.png");
        await Assert.That(a).IsNotEqualTo(b);
    }

    /// <summary>Trailing slashes are trimmed from the configured asset directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingSlashTrimmed()
    {
        var registry = new ExternalAssetRegistry("assets/external/");
        var local = registry.GetOrAdd("https://x.test/a.png");
        await Assert.That(local).StartsWith("assets/external/");
        await Assert.That(local.StartsWith("assets/external//", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>EntriesSnapshot exposes the registered (url, localPath) pairs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EntriesSnapshotReflectsRegistrations()
    {
        var registry = new ExternalAssetRegistry("d");
        registry.GetOrAdd("https://x.test/a.png");
        registry.GetOrAdd("https://x.test/b.css");
        var entries = registry.EntriesSnapshot();
        await Assert.That(entries.Length).IsEqualTo(2);
    }

    /// <summary>UrlsSnapshot returns just the URL component of every entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlsSnapshotReturnsUrlsOnly()
    {
        var registry = new ExternalAssetRegistry("d");
        registry.GetOrAdd("https://x.test/a.png");
        var urls = registry.UrlsSnapshot();
        await Assert.That(urls).Contains("https://x.test/a.png");
    }
}
