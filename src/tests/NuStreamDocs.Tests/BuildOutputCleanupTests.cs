// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;
using NuStreamDocs.Caching;
using NuStreamDocs.Common;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Tests;

/// <summary>Output ownership survives cache invalidation without granting access outside the site.</summary>
public sealed class BuildOutputCleanupTests
{
    /// <summary>Authored HTML asset used in replacement checks.</summary>
    private const string AssetFileName = "asset.html";

    /// <summary>Owned page path used in inventory round trips.</summary>
    private const string PageOutputPath = "page/index.html";

    /// <summary>Output length recorded in the manifest fixture.</summary>
    private const int OutputLength = 10;

    /// <summary>Recorded output inventory for the generated page.</summary>
    private static readonly FilePath[] _pageOutputs = [PageOutputPath];

    /// <summary>Recorded output inventory for an authored asset takeover.</summary>
    private static readonly FilePath[] _assetOutputs = [AssetFileName];

    /// <summary>A changed pipeline retains its recorded output paths for cleanup.</summary>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task ChangedFingerprintRetainsOutputOwnership(CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        var manifest = BuildManifest.Empty([.. "build-a"u8]);
        manifest.Replace([new("page.md", [.. "source"u8], OutputLength)]);
        manifest.SetOutputPaths(_pageOutputs);
        await manifest.SaveAsync(fixture.Output, cancellationToken);

        var loaded = await BuildManifest.LoadAsync(fixture.Output, [.. "build-b"u8], cancellationToken);

        await Assert.That(loaded.Count).IsEqualTo(0);
        await Assert.That(loaded.GetOutputPaths().Length).IsEqualTo(1);
        await Assert.That(loaded.GetOutputPaths()[0].Value).IsEqualTo(PageOutputPath);
    }

    /// <summary>A recorded path cannot delete files outside the output root.</summary>
    /// <param name="absolute">Whether the record contains an absolute path.</param>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RecordedPathsCannotEscapeOutput(bool absolute, CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        _ = Directory.CreateDirectory(fixture.Output);
        var outside = Path.Combine(fixture.Root, "outside.html");
        await File.WriteAllTextAsync(outside, "Keep this file", cancellationToken);
        FilePath recorded = absolute ? outside : "../outside.html";
        var registry = new PageOutputRegistry(fixture.Output);
        var shell = new BuildPhaseShell(fixture.Input, fixture.Output, BuildPipelineOptions.Default, new PluginTimingTable(), NullLogger.Instance);

        BuildOutputCleanup.RemoveObsolete([recorded], [], registry.Comparer, shell);

        await Assert.That(await File.ReadAllTextAsync(outside, cancellationToken)).IsEqualTo("Keep this file");
    }

    /// <summary>An authored HTML asset is retained when it replaces a generated page.</summary>
    /// <param name="cancellationToken">Test cancellation.</param>
    /// <returns>The asynchronous assertions.</returns>
    [Test]
    public async Task AuthoredAssetsAreRetained(CancellationToken cancellationToken)
    {
        using var fixture = TempBuildFixture.Create();
        _ = Directory.CreateDirectory(fixture.Output);
        var output = Path.Combine(fixture.Output, AssetFileName);
        await File.WriteAllTextAsync(output, "Existing asset", cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, AssetFileName), "Authored asset", cancellationToken);
        var registry = new PageOutputRegistry(fixture.Output);
        var shell = new BuildPhaseShell(fixture.Input, fixture.Output, BuildPipelineOptions.Default, new PluginTimingTable(), NullLogger.Instance);

        BuildOutputCleanup.RemoveObsolete(_assetOutputs, [], registry.Comparer, shell);

        await Assert.That(await File.ReadAllTextAsync(output, cancellationToken)).IsEqualTo("Existing asset");
    }
}
