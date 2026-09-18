// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using SourceDocParser;
using SourceDocParser.Model;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Direct tests for the previously private helpers in CSharpApiGenerator and AssemblySourceFactory.</summary>
public class CSharpApiGeneratorHelperTests
{
    /// <summary>Cache Directory used by the test cases.</summary>
    private const string CacheDirectory = "/cache";

    /// <summary>Target Framework used by the test cases.</summary>
    private const string TargetFramework = "net10.0";

    /// <summary>Package Count used by the test cases.</summary>
    private const int PackageCount = 2;

    /// <summary>DescribeInput renders each shape with its diagnostic shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DescribeInputShapes()
    {
        await Assert.That(CSharpApiGenerator.DescribeInput(new NuGetManifestInput("/repo", CacheDirectory)))
            .IsEqualTo("manifest:/repo");
        await Assert.That(CSharpApiGenerator.DescribeInput(new NuGetPackagesInput([new("Foo", "1.0")], CacheDirectory)))
            .IsEqualTo("packages:1");
        await Assert.That(CSharpApiGenerator.DescribeInput(new LocalAssembliesInput(TargetFramework, ["/a.dll", "/b.dll"])))
            .IsEqualTo("assemblies:2@net10.0");
        await Assert.That(CSharpApiGenerator.DescribeInput(new CustomInput(new EmptySource())))
            .IsEqualTo("custom-source");
    }

    /// <summary>DescribeInputs returns the single label when only one input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DescribeInputsSingle()
    {
        var label = CSharpApiGenerator.DescribeInputs([new NuGetManifestInput("/r", "/c")]);
        await Assert.That(label).IsEqualTo("manifest:/r");
    }

    /// <summary>DescribeInputs joins multiple shapes with commas.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DescribeInputsMultiple()
    {
        var label = CSharpApiGenerator.DescribeInputs(
        [
            new NuGetManifestInput("/r", "/c"),
            new LocalAssembliesInput(TargetFramework, ["/x.dll"])
        ]);
        await Assert.That(label).IsEqualTo("manifest:/r,assemblies:1@net10.0");
    }

    /// <summary>BuildManifestJson emits the expected nuget-packages.json shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildManifestJsonShape()
    {
        var bytes = AssemblySourceFactory.BuildManifestJson(
            new([new("Foo", "1.2.3"), new("Bar", "4.5")], CacheDirectory));
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        await Assert.That(root.GetProperty("nugetPackageOwners").GetArrayLength()).IsEqualTo(0);
        await Assert.That(root.GetProperty("tfmPreference").GetArrayLength()).IsGreaterThan(0);
        var pkgs = root.GetProperty("additionalPackages");
        await Assert.That(pkgs.GetArrayLength()).IsEqualTo(PackageCount);
        await Assert.That(pkgs[0].GetProperty("id").GetString()).IsEqualTo("Foo");
        await Assert.That(pkgs[0].GetProperty("version").GetString()).IsEqualTo("1.2.3");
    }

    /// <summary>Package versions and framework selections retain independent manifests in a shared cache.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task InlinePackageInputsKeepIndependentManifests()
    {
        var cache = Path.Combine(Path.GetTempPath(), $"smkd-manifests-{Guid.NewGuid():N}");
        var first = new NuGetPackagesInput([new("Fixture", "1.0.0")], cache, [TargetFramework]);
        NuGetPackagesInput[] inputs =
        [
            first,
            first with { Packages = [new("Fixture", "2.0.0")] },
            first with { TfmPreference = ["netstandard2.0"] }
        ];
        try
        {
            foreach (var input in inputs)
            {
                using var source = AssemblySourceFactory.CreateFromPackages(input, NullLogger.Instance);
            }

            using var repeated = AssemblySourceFactory.CreateFromPackages(first, NullLogger.Instance);
            var files = Directory.GetFiles(cache, "nuget-packages.json", SearchOption.AllDirectories);
            await Assert.That(files.Length).IsEqualTo(inputs.Length);
            HashSet<string> manifests = [with(StringComparer.Ordinal)];
            foreach (var file in files)
            {
                _ = manifests.Add(await File.ReadAllTextAsync(file));
            }

            foreach (var input in inputs)
            {
                await Assert.That(manifests).Contains(Encoding.UTF8.GetString(AssemblySourceFactory.BuildManifestJson(input)));
            }
        }
        finally
        {
            Directory.Delete(cache, true);
        }
    }

    /// <summary>CreateOne dispatches to the right source per input shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateOneCustom()
    {
        EmptySource src = new();
        var resolved = AssemblySourceFactory.CreateOne(new CustomInput(src), NullLogger.Instance);
        await Assert.That(resolved).IsEqualTo(src);
    }

    /// <summary>CreateOne resolves a local-assemblies input to a LocalAssemblySource.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateOneLocalAssemblies()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"smkd-cln-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(dir);
        try
        {
            var resolved = AssemblySourceFactory.CreateOne(
                new LocalAssembliesInput(TargetFramework, []),
                NullLogger.Instance);
            await Assert.That(resolved).IsTypeOf<LocalAssemblySource>();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    /// <summary>Stub IAssemblySource that yields no groups.</summary>
    private sealed class EmptySource : IAssemblySource
    {
        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IAsyncEnumerable<AssemblyGroup> DiscoverAsync() => DiscoverAsync(CancellationToken.None);

        /// <inheritdoc/>
        public async IAsyncEnumerable<AssemblyGroup> DiscoverAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }
    }
}
