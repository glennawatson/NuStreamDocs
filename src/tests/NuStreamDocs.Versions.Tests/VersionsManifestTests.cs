// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;

namespace NuStreamDocs.Versions.Tests;

/// <summary>Behavior tests for <c>VersionsManifest</c>.</summary>
public class VersionsManifestTests
{
    /// <summary>Version retained for compatibility with legacy documentation.</summary>
    private const string LegacyVersion = "0.1.0";

    /// <summary>Version identified as the latest release.</summary>
    private const string LatestVersion = "0.4.2";

    /// <summary>Display title for the latest release.</summary>
    private const string LatestTitle = "0.4 (latest)";

    /// <summary>Gets the alias identifying the latest release.</summary>
    private static ReadOnlySpan<byte> LatestAlias => "latest"u8;

    /// <summary>A round-trip via UTF-8 preserves the entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RoundTripUtf8PreservesEntries()
    {
        VersionEntry[] input =
        [
            new(LegacyVersion, "0.1 (legacy)", []),
            new(LatestVersion, LatestTitle, [[.. LatestAlias], [.. "stable"u8]])
        ];

        ArrayBufferWriter<byte> sink = new();
        VersionsManifest.WriteToUtf8(input, sink);
        var roundTripped = VersionsManifest.ReadFromUtf8(sink.WrittenSpan);

        await Assert.That(roundTripped.Length).IsEqualTo(input.Length);
        await Assert.That(roundTripped[0].Version).IsEqualTo(LegacyVersion);
        await Assert.That(roundTripped[1].Aliases.Length).IsEqualTo(input[1].Aliases.Length);
        await Assert.That(Encoding.UTF8.GetString(roundTripped[1].Aliases[0])).IsEqualTo("latest");
    }

    /// <summary><c>VersionsManifest.Upsert</c> replaces an entry when the version matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UpsertReplacesMatchingVersion()
    {
        VersionEntry[] existing =
        [
            new(LegacyVersion, "0.1", []),
            new(LatestVersion, "0.4 (old)", [])
        ];

        var merged = VersionsManifest.Upsert(existing, new(LatestVersion, LatestTitle, [[.. LatestAlias]]));

        await Assert.That(merged.Length).IsEqualTo(existing.Length);
        await Assert.That(merged[1].Title).IsEqualTo(LatestTitle);
        await Assert.That(merged[1].Aliases.Length).IsEqualTo(1);
    }

    /// <summary><c>VersionsManifest.Upsert</c> appends when no entry matches.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UpsertAppendsNewVersion()
    {
        VersionEntry[] existing = [new(LegacyVersion, "0.1", [])];
        var merged = VersionsManifest.Upsert(existing, new(LatestVersion, "0.4", [[.. LatestAlias]]));

        await Assert.That(merged.Length).IsEqualTo(existing.Length + 1);
        await Assert.That(merged[1].Version).IsEqualTo(LatestVersion);
    }

    /// <summary>Reading an empty span yields no entries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadFromMissingFileYieldsEmpty()
    {
        var dir = Path.Combine(
            Path.GetTempPath(),
            $"smd-versions-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)}");
        try
        {
            _ = Directory.CreateDirectory(dir);
            var entries = VersionsManifest.Read(dir);
            await Assert.That(entries.Length).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>Unknown JSON properties on an entry are skipped without error.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ReadIgnoresUnknownProperties()
    {
        var entries = VersionsManifest.ReadFromUtf8("""[{"version":"0.1.0","title":"0.1","aliases":[],"docVersion":"foo"}]"""u8);

        await Assert.That(entries.Length).IsEqualTo(1);
        await Assert.That(entries[0].Version).IsEqualTo(LegacyVersion);
    }
}
