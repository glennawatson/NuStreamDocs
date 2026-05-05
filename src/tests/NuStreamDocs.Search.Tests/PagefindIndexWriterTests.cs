// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Tests;

/// <summary>Branch-coverage tests for the Pagefind manifest+record writer.</summary>
public class PagefindIndexWriterTests
{
    /// <summary>An empty corpus emits a manifest with an empty records array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyCorpusWritesManifestOnly()
    {
        using TempDir dir = new();
        PagefindIndexWriter.Write(dir.Root, []);
        var manifest = await File.ReadAllTextAsync(Path.Combine(dir.Root, "pagefind-entry.json"));
        await Assert.That(manifest).Contains("\"version\":\"1.4.0\"");
        await Assert.That(manifest).Contains("\"records\":[]");
    }

    /// <summary>Each document gets a record file named after its safe-char slug.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SafeCharsKeptInSlug()
    {
        using TempDir dir = new();
        PagefindIndexWriter.Write(
            dir.Root,
            [new([.. "abc_123"u8], [.. "T"u8], [.. "B"u8])]);
        await Assert.That(File.Exists(Path.Combine(dir.Root, "pagefind-records", "abc_123.json"))).IsTrue();
    }

    /// <summary>Unsafe URL bytes are replaced with hyphens.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnsafeCharsBecomeHyphen()
    {
        using TempDir dir = new();
        PagefindIndexWriter.Write(
            dir.Root,
            [new([.. "a/b.html"u8], [.. "T"u8], [.. "B"u8])]);
        await Assert.That(File.Exists(Path.Combine(dir.Root, "pagefind-records", "a-b-html.json"))).IsTrue();
    }

    /// <summary>An empty URL falls back to the page-N slug.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyUrlFallsBackToOrdinal()
    {
        using TempDir dir = new();
        PagefindIndexWriter.Write(
            dir.Root,
            [new([], [.. "T"u8], [.. "B"u8])]);
        await Assert.That(File.Exists(Path.Combine(dir.Root, "pagefind-records", "page-0.json"))).IsTrue();
    }

    /// <summary>The per-record file carries url + title + content fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PerRecordPayloadShape()
    {
        using TempDir dir = new();
        PagefindIndexWriter.Write(
            dir.Root,
            [new([.. "page"u8], [.. "Hi"u8], [.. "Body"u8])]);
        var record = await File.ReadAllTextAsync(Path.Combine(dir.Root, "pagefind-records", "page.json"));
        await Assert.That(record).Contains("\"url\":\"page\"");
        await Assert.That(record).Contains("\"title\":\"Hi\"");
        await Assert.That(record).Contains("\"content\":\"Body\"");
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempDir"/> class.</summary>
        public TempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-pf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch root.</summary>
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
