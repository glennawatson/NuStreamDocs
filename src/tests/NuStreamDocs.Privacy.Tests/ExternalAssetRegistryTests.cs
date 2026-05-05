// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Privacy.Tests;

/// <summary>Branch-coverage tests for ExternalAssetRegistry.</summary>
public class ExternalAssetRegistryTests
{
    /// <summary>Standard test asset directory text; reused across registry instances.</summary>
    private const string AssetDirText = "assets/external";

    /// <summary>Asset directory + trailing slash, the prefix every produced local path starts with.</summary>
    private const string AssetDirPrefix = AssetDirText + "/";

    /// <summary>Test PNG URL — used across the byte-overload, idempotency, and snapshot tests.</summary>
    private const string PngUrl = "https://x.test/a.png";

    /// <summary>UTF-8 form of <see cref="AssetDirText"/> for the byte-shaped registry constructor.</summary>
    private static readonly byte[] AssetDir = Encoding.UTF8.GetBytes(AssetDirText);

    /// <summary>UTF-8 byte form of <c>"d"</c>, the throwaway asset directory used by idempotency / parity tests.</summary>
    private static readonly byte[] DDir = [.. "d"u8];

    /// <summary>UTF-8 form of <see cref="AssetDirPrefix"/> for the trailing-slash-trimming test.</summary>
    private static readonly byte[] AssetDirPrefixBytes = Encoding.UTF8.GetBytes(AssetDirPrefix);

    /// <summary>Byte overload preserves the file extension byte-for-byte.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadPreservesExtension()
    {
        ExternalAssetRegistry registry = new(AssetDir);
        var local = registry.GetOrAdd("https://x.test/a/b.css"u8);
        await Assert.That(local.AsSpan().EndsWith(".css"u8)).IsTrue();
        await Assert.That(local.AsSpan().StartsWith(Encoding.UTF8.GetBytes(AssetDirPrefix))).IsTrue();
    }

    /// <summary>Empty byte input throws at the boundary.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadRejectsEmpty()
    {
        ExternalAssetRegistry registry = new(AssetDir);
        await Assert.That(() => registry.GetOrAdd(default(ReadOnlySpan<byte>).ToArray()))
            .Throws<ArgumentException>();
    }

    /// <summary>Byte overload is idempotent — same URL returns the same byte[] on the second call.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ByteOverloadIsIdempotent()
    {
        ExternalAssetRegistry registry = new(AssetDir);
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
        ExternalAssetRegistry registry = new(AssetDir);
        var local = Encoding.UTF8.GetString(registry.GetOrAdd(Encoding.UTF8.GetBytes(url)));
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
        ExternalAssetRegistry registry = new(AssetDir);
        var local = Encoding.UTF8.GetString(registry.GetOrAdd(Encoding.UTF8.GetBytes(url)));
        await Assert.That(local).StartsWith(AssetDirPrefix);
        await Assert.That(local.Contains('.', StringComparison.Ordinal) && local.LastIndexOf('.') > AssetDirPrefix.Length - 1).IsFalse();
    }

    /// <summary>The same URL maps to the same local path on subsequent calls.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GetOrAddIsIdempotent()
    {
        ExternalAssetRegistry registry = new(DDir);
        var a = registry.GetOrAdd("https://x.test/a.png"u8);
        var b = registry.GetOrAdd("https://x.test/a.png"u8);
        await Assert.That(a.AsSpan().SequenceEqual(b)).IsTrue();
    }

    /// <summary>Different URLs map to different local paths (hash-driven).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DifferentUrlsDistinctPaths()
    {
        ExternalAssetRegistry registry = new(DDir);
        var a = registry.GetOrAdd("https://x.test/a.png"u8);
        var b = registry.GetOrAdd("https://x.test/b.png"u8);
        await Assert.That(a.AsSpan().SequenceEqual(b)).IsFalse();
    }

    /// <summary>Trailing slashes are trimmed from the configured asset directory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TrailingSlashTrimmed()
    {
        ExternalAssetRegistry registry = new(AssetDirPrefixBytes);
        var local = Encoding.UTF8.GetString(registry.GetOrAdd("https://x.test/a.png"u8));
        await Assert.That(local).StartsWith(AssetDirPrefix);
        await Assert.That(local.StartsWith("assets/external//", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>EntriesSnapshot exposes the registered (url, localPath) pairs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EntriesSnapshotReflectsRegistrations()
    {
        ExternalAssetRegistry registry = new(DDir);
        registry.GetOrAdd("https://x.test/a.png"u8);
        registry.GetOrAdd("https://x.test/b.css"u8);
        var entries = registry.EntriesSnapshot();
        await Assert.That(entries.Length).IsEqualTo(2);
    }

    /// <summary>UrlsSnapshot returns just the URL component of every entry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UrlsSnapshotReturnsUrlsOnly()
    {
        ExternalAssetRegistry registry = new(DDir);
        registry.GetOrAdd("https://x.test/a.png"u8);
        var urls = registry.UrlsSnapshot();
        await Assert.That(urls.Length).IsEqualTo(1);
        await Assert.That(urls[0].AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(PngUrl))).IsTrue();
    }
}
