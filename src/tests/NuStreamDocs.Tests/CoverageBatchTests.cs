// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Caching;

namespace NuStreamDocs.Tests;

/// <summary>Direct coverage for small core helpers that lack dedicated tests.</summary>
public class CoverageBatchTests
{
    /// <summary>ContentHasher.EmptyHex returns the canonical empty-content hex string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ContentHasherEmptyHex()
    {
        var empty = ContentHasher.EmptyHex();
        await Assert.That(empty).IsEqualTo(new('0', empty.Length));
        await Assert.That(empty.Length).IsGreaterThan(0);
    }

    /// <summary>Default PageBuilderPool.Rent() yields a non-null writer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PageBuilderPoolDefaultRent()
    {
        using var rental = PageBuilderPool.Rent();
        await Assert.That(rental.Writer).IsNotNull();
    }

    /// <summary>BuildManifest round-trips through SaveAsync and LoadAsync.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildManifestRoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smkd-mf-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = BuildManifest.Empty();
            manifest.Replace([new("a.md", "deadbeef", 42L)]);
            await manifest.SaveAsync(dir, CancellationToken.None);
            await Assert.That(manifest.Count).IsEqualTo(1);

            var loaded = await BuildManifest.LoadAsync(dir, CancellationToken.None);
            await Assert.That(loaded.Count).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>BuildPipeline.RunAsync(input, output, plugins) accepts an empty input dir.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildPipelineRunAsyncOverloads()
    {
        var input = Path.Combine(Path.GetTempPath(), "smkd-bp-in-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "smkd-bp-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(input);
        try
        {
            await BuildPipeline.RunAsync(input, output, []);
            await BuildPipeline.RunAsync(input, output, [], CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(input))
            {
                Directory.Delete(input, recursive: true);
            }

            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    /// <summary>PageDiscovery.EnumerateAsync overloads enumerate empty directories.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PageDiscoveryEnumerateAsync()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smkd-pd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var count = 0;
            await foreach (var p in PageDiscovery.EnumerateAsync(dir))
            {
                count++;
            }

            await foreach (var p in PageDiscovery.EnumerateAsync(dir, CancellationToken.None))
            {
                count++;
            }

            await Assert.That(count).IsEqualTo(0);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
