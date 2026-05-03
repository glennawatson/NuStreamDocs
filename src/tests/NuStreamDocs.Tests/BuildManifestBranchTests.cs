// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using NuStreamDocs.Caching;

namespace NuStreamDocs.Tests;

/// <summary>Branch-coverage tests for BuildManifest.</summary>
public class BuildManifestBranchTests
{
    /// <summary>LoadAsync returns Empty when no manifest file exists.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoadAsyncMissingFile()
    {
        using var temp = new ScratchDir();
        var manifest = await BuildManifest.LoadAsync(temp.Root, CancellationToken.None);
        await Assert.That(manifest.Count).IsEqualTo(0);
    }

    /// <summary>LoadAsync returns Empty for a corrupt manifest file.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoadAsyncCorruptFile()
    {
        using var temp = new ScratchDir();
        var path = Path.Combine(temp.Root, BuildManifest.FileName);
        await File.WriteAllTextAsync(path, "{not json");
        var manifest = await BuildManifest.LoadAsync(temp.Root, CancellationToken.None);
        await Assert.That(manifest.Count).IsEqualTo(0);
    }

    /// <summary>Replace(queue) drains and indexes entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReplaceFromQueue()
    {
        var manifest = BuildManifest.Empty();
        var queue = new ConcurrentQueue<ManifestEntry>();
        var aHash = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var bHash = new byte[] { 9, 10, 11, 12, 13, 14, 15, 16 };
        queue.Enqueue(new("a.md", aHash, 10));
        queue.Enqueue(new("b.md", bHash, 20));
        manifest.Replace(queue);
        await Assert.That(manifest.Count).IsEqualTo(2);
        await Assert.That(manifest.TryGet("a.md", out var entry)).IsTrue();
        await Assert.That(entry.ContentHash.AsSpan().SequenceEqual(aHash)).IsTrue();
    }

    /// <summary>TryGet returns false for unknown paths.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TryGetMiss()
    {
        var manifest = BuildManifest.Empty();
        await Assert.That(manifest.TryGet("missing.md", out _)).IsFalse();
    }

    /// <summary>SaveAsync + LoadAsync round-trip preserves entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SaveLoadRoundTrip()
    {
        using var temp = new ScratchDir();
        var manifest = BuildManifest.Empty("test-build");
        var hash = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x01, 0x02, 0x03, 0x04 };
        manifest.Replace([new("p.md", hash, 99)]);
        await manifest.SaveAsync(temp.Root, CancellationToken.None);
        var loaded = await BuildManifest.LoadAsync(temp.Root, CancellationToken.None);
        await Assert.That(loaded.Count).IsEqualTo(1);
        await Assert.That(loaded.TryGet("p.md", out var entry)).IsTrue();
        await Assert.That(entry.ContentHash.AsSpan().SequenceEqual(hash)).IsTrue();
        await Assert.That(entry.OutputLengthBytes).IsEqualTo(99);
    }

    /// <summary>A build fingerprint mismatch forces a cold-cache manifest.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task LoadAsyncRejectsDifferentBuildFingerprint()
    {
        using var temp = new ScratchDir();
        var manifest = BuildManifest.Empty("build-a");
        manifest.Replace([new("p.md", [1, 2, 3, 4], 10)]);
        await manifest.SaveAsync(temp.Root, CancellationToken.None);

        var loaded = await BuildManifest.LoadAsync(temp.Root, "build-b", CancellationToken.None);
        await Assert.That(loaded.Count).IsEqualTo(0);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-bm-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch directory.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
