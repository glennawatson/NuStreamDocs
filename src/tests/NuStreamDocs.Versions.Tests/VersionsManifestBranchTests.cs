// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Versions.Tests;

/// <summary>Branch-coverage tests for VersionsManifest read/write/upsert paths.</summary>
public class VersionsManifestBranchTests
{
    /// <summary>Read returns empty when the parent directory has no versions.json.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadMissingFile()
    {
        using var temp = new ScratchDir();
        await Assert.That(VersionsManifest.Read(temp.Root).Length).IsEqualTo(0);
    }

    /// <summary>ReadFromUtf8 returns empty when the JSON isn't an array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadFromUtf8NonArray()
    {
        var bytes = "{\"x\":1}"u8.ToArray();
        await Assert.That(VersionsManifest.ReadFromUtf8(bytes).Length).IsEqualTo(0);
    }

    /// <summary>Write + Read round-trip preserves entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RoundTrip()
    {
        using var temp = new ScratchDir();
        VersionEntry[] entries = [new("1.0", "Stable", ["latest"]), new("2.0", "Next", [])];
        VersionsManifest.Write(temp.Root, entries);
        var roundTripped = VersionsManifest.Read(temp.Root);
        await Assert.That(roundTripped.Length).IsEqualTo(2);
        await Assert.That(roundTripped[0].Version).IsEqualTo("1.0");
        await Assert.That(roundTripped[0].Aliases.Length).IsEqualTo(1);
        await Assert.That(roundTripped[1].Version).IsEqualTo("2.0");
    }

    /// <summary>Upsert appends a new version when not already present.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UpsertAppendsNew()
    {
        VersionEntry[] existing = [new("1.0", "A", [])];
        var merged = VersionsManifest.Upsert(existing, new("2.0", "B", []));
        await Assert.That(merged.Length).IsEqualTo(2);
        await Assert.That(merged[1].Version).IsEqualTo("2.0");
    }

    /// <summary>Upsert replaces in place when version matches.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UpsertReplaces()
    {
        VersionEntry[] existing = [new("1.0", "Old", []), new("2.0", "Untouched", [])];
        var merged = VersionsManifest.Upsert(existing, new("1.0", "New", ["latest"]));
        await Assert.That(merged.Length).IsEqualTo(2);
        await Assert.That(merged[0].Title).IsEqualTo("New");
        await Assert.That(merged[1].Title).IsEqualTo("Untouched");
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-vm-" + Guid.NewGuid().ToString("N"));
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
