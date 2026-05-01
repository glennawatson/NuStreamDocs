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
        queue.Enqueue(new("a.md", "hash1", 10));
        queue.Enqueue(new("b.md", "hash2", 20));
        manifest.Replace(queue);
        await Assert.That(manifest.Count).IsEqualTo(2);
        await Assert.That(manifest.TryGet("a.md", out var entry)).IsTrue();
        await Assert.That(entry.ContentHash).IsEqualTo("hash1");
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
        var manifest = BuildManifest.Empty();
        manifest.Replace([new("p.md", "deadbeef", 99)]);
        await manifest.SaveAsync(temp.Root, CancellationToken.None);
        var loaded = await BuildManifest.LoadAsync(temp.Root, CancellationToken.None);
        await Assert.That(loaded.Count).IsEqualTo(1);
        await Assert.That(loaded.TryGet("p.md", out var entry)).IsTrue();
        await Assert.That(entry.ContentHash).IsEqualTo("deadbeef");
        await Assert.That(entry.OutputLengthBytes).IsEqualTo(99);
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
