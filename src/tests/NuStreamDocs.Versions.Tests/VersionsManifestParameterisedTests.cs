// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Versions.Tests;

/// <summary>Parameterised round-trip / read-tolerance tests for VersionsManifest.</summary>
public class VersionsManifestParameterisedTests
{
    /// <summary>Each entry shape (with/without aliases) round-trips cleanly.</summary>
    /// <param name="version">Version string.</param>
    /// <param name="title">Title.</param>
    /// <param name="aliasCount">Number of aliases on the entry.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("1.0", "Stable", 0)]
    [Arguments("1.1", "Latest", 1)]
    [Arguments("2.0", "Next", 2)]
    [Arguments("3.0", "With spaces in title", 1)]
    public async Task RoundTripShapes(string version, string title, int aliasCount)
    {
        using var temp = new ScratchDir();
        var aliases = Enumerable.Range(0, aliasCount).Select(i => $"alias{i}").ToArray();
        VersionEntry[] entries = [new(version, title, aliases)];
        VersionsManifest.Write(temp.Root, entries);
        var loaded = VersionsManifest.Read(temp.Root);
        await Assert.That(loaded.Length).IsEqualTo(1);
        await Assert.That(loaded[0].Version).IsEqualTo(version);
        await Assert.That(loaded[0].Title).IsEqualTo(title);
        await Assert.That(loaded[0].Aliases.Length).IsEqualTo(aliasCount);
    }

    /// <summary>Read tolerates malformed JSON shapes by returning an empty array.</summary>
    /// <param name="json">On-disk JSON contents.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("{}")]
    [Arguments("\"a string\"")]
    [Arguments("null")]
    public async Task ReadFromUtf8MalformedShapes(string json) =>
        await Assert.That(VersionsManifest.ReadFromUtf8(Encoding.UTF8.GetBytes(json)).Length).IsEqualTo(0);

    /// <summary>Upsert with the same version replaces in place; a new version appends.</summary>
    /// <param name="newVersion">Version to upsert.</param>
    /// <param name="expectedLength">Expected resulting array length.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("1.0", 2)]
    [Arguments("2.0", 2)]
    [Arguments("3.0", 3)]
    public async Task UpsertAddOrReplace(string newVersion, int expectedLength)
    {
        VersionEntry[] existing = [new("1.0", "A", []), new("2.0", "B", [])];
        var merged = VersionsManifest.Upsert(existing, new(newVersion, "Updated", []));
        await Assert.That(merged.Length).IsEqualTo(expectedLength);
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-vmp-" + Guid.NewGuid().ToString("N"));
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
