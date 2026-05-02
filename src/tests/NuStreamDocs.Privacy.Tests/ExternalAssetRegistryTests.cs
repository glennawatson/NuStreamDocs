// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Branch-coverage tests for ExternalAssetRegistry.</summary>
public class ExternalAssetRegistryTests
{
    /// <summary>Standard test asset directory; reused across registry instances.</summary>
    private const string AssetDir = "assets/external";

    /// <summary>Asset directory + trailing slash, the prefix every produced local path starts with.</summary>
    private const string AssetDirPrefix = AssetDir + "/";

    /// <summary>Test PNG URL — used across the byte/string overload parity, idempotency, and snapshot tests.</summary>
    private const string PngUrl = "https://x.test/a.png";

    /// <summary>Byte overload matches the string overload for a typical URL — adapter parity.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadMatchesStringOverload()
    {
        var registry = new ExternalAssetRegistry(AssetDir);
        var fromString = registry.GetOrAdd(PngUrl);
        var fromBytes = registry.GetOrAdd("https://x.test/a.png"u8);
        await Assert.That(Encoding.UTF8.GetString(fromBytes)).IsEqualTo(fromString);
    }

    /// <summary>Byte overload preserves the file extension byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadPreservesExtension()
    {
        var registry = new ExternalAssetRegistry(AssetDir);
        var local = registry.GetOrAdd("https://x.test/a/b.css"u8);
        await Assert.That(local.AsSpan().EndsWith(".css"u8)).IsTrue();
        await Assert.That(local.AsSpan().StartsWith(Encoding.UTF8.GetBytes(AssetDirPrefix))).IsTrue();
    }

    /// <summary>Empty byte input throws at the boundary (matches the string overload).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadRejectsEmpty()
    {
        var registry = new ExternalAssetRegistry(AssetDir);
        await Assert.That(() => registry.GetOrAdd(default(ReadOnlySpan<byte>).ToArray()))
            .Throws<ArgumentException>();
    }

    /// <summary>Byte overload is idempotent — same URL returns the same byte[] on the second call.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadIsIdempotent()
    {
        var registry = new ExternalAssetRegistry(AssetDir);
        var a = registry.GetOrAdd("https://x.test/a.png"u8);
        var b = registry.GetOrAdd("https://x.test/a.png"u8);
        await Assert.That(a.AsSpan().SequenceEqual(b)).IsTrue();
    }

    /// <summary>Recognized file extensions are preserved on the local path.</summary>
    /// <param name="url">Source URL.</param>
    /// <param name="expectedExt">Expected suffix on the registered path.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(PngUrl, ".png")]
    [Arguments("https://x.test/a/b.css", ".css")]
    [Arguments("https://x.test/a.WOFF2", ".WOFF2")]
    [Arguments("https://x.test/a.svg?v=1", ".svg")]
    public async Task RecognizedExtensionPreserved(string url, string expectedExt)
    {
        var registry = new ExternalAssetRegistry(AssetDir);
        var local = registry.GetOrAdd(url);
        await Assert.That(local).EndsWith(expectedExt);
        await Assert.That(local).StartsWith(AssetDirPrefix);
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
        var registry = new ExternalAssetRegistry(AssetDir);
        var local = registry.GetOrAdd(url);
        await Assert.That(local).StartsWith(AssetDirPrefix);
        await Assert.That(local.Contains('.', StringComparison.Ordinal) && local.LastIndexOf('.') > AssetDirPrefix.Length - 1).IsFalse();
    }

    /// <summary>The same URL maps to the same local path on subsequent calls.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GetOrAddIsIdempotent()
    {
        var registry = new ExternalAssetRegistry("d");
        var a = registry.GetOrAdd(PngUrl);
        var b = registry.GetOrAdd(PngUrl);
        await Assert.That(a).IsEqualTo(b);
    }

    /// <summary>Different URLs map to different local paths (hash-driven).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DifferentUrlsDistinctPaths()
    {
        var registry = new ExternalAssetRegistry("d");
        var a = registry.GetOrAdd(PngUrl);
        var b = registry.GetOrAdd("https://x.test/b.png");
        await Assert.That(a).IsNotEqualTo(b);
    }

    /// <summary>Trailing slashes are trimmed from the configured asset directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingSlashTrimmed()
    {
        var registry = new ExternalAssetRegistry(AssetDirPrefix);
        var local = registry.GetOrAdd(PngUrl);
        await Assert.That(local).StartsWith(AssetDirPrefix);
        await Assert.That(local.StartsWith("assets/external//", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>EntriesSnapshot exposes the registered (url, localPath) pairs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EntriesSnapshotReflectsRegistrations()
    {
        var registry = new ExternalAssetRegistry("d");
        registry.GetOrAdd(PngUrl);
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
        registry.GetOrAdd(PngUrl);
        var urls = registry.UrlsSnapshot();
        await Assert.That(urls).Contains(PngUrl);
    }
}
