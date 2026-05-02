// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;

namespace NuStreamDocs.Versions.Tests;

/// <summary>Behavior tests for <c>VersionsManifest</c>.</summary>
public class VersionsManifestTests
{
    /// <summary>A round-trip via UTF-8 preserves the entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RoundTripUtf8PreservesEntries()
    {
        VersionEntry[] input = [
            new("0.1.0", "0.1 (legacy)", []),
            new("0.4.2", "0.4 (latest)", ["latest", "stable"]),
        ];

        var sink = new ArrayBufferWriter<byte>();
        VersionsManifest.WriteToUtf8(input, sink);
        var roundTripped = VersionsManifest.ReadFromUtf8(sink.WrittenSpan);

        await Assert.That(roundTripped.Length).IsEqualTo(2);
        await Assert.That(roundTripped[0].Version).IsEqualTo("0.1.0");
        await Assert.That(roundTripped[1].Aliases.Length).IsEqualTo(2);
        await Assert.That(roundTripped[1].Aliases[0]).IsEqualTo("latest");
    }

    /// <summary><c>VersionsManifest.Upsert</c> replaces an entry when the version matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UpsertReplacesMatchingVersion()
    {
        VersionEntry[] existing = [
            new("0.1.0", "0.1", []),
            new("0.4.2", "0.4 (old)", []),
        ];

        var merged = VersionsManifest.Upsert(existing, new("0.4.2", "0.4 (latest)", ["latest"]));

        await Assert.That(merged.Length).IsEqualTo(2);
        await Assert.That(merged[1].Title).IsEqualTo("0.4 (latest)");
        await Assert.That(merged[1].Aliases.Length).IsEqualTo(1);
    }

    /// <summary><c>VersionsManifest.Upsert</c> appends when no entry matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UpsertAppendsNewVersion()
    {
        VersionEntry[] existing = [new("0.1.0", "0.1", [])];
        var merged = VersionsManifest.Upsert(existing, new("0.4.2", "0.4", ["latest"]));

        await Assert.That(merged.Length).IsEqualTo(2);
        await Assert.That(merged[1].Version).IsEqualTo("0.4.2");
    }

    /// <summary>Reading an empty span yields no entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadFromMissingFileYieldsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smd-versions-" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture));
        try
        {
            Directory.CreateDirectory(dir);
            var entries = VersionsManifest.Read(dir);
            await Assert.That(entries.Length).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Unknown JSON properties on an entry are skipped without error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadIgnoresUnknownProperties()
    {
        const string Json = """[{"version":"0.1.0","title":"0.1","aliases":[],"docVersion":"foo"}]""";
        var entries = VersionsManifest.ReadFromUtf8(Encoding.UTF8.GetBytes(Json));

        await Assert.That(entries.Length).IsEqualTo(1);
        await Assert.That(entries[0].Version).IsEqualTo("0.1.0");
    }
}
