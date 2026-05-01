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
    /// <summary>DescribeInput renders each shape with its diagnostic shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DescribeInputShapes()
    {
        await Assert.That(CSharpApiGenerator.DescribeInput(new NuGetManifestInput("/repo", "/cache"))).IsEqualTo("manifest:/repo");
        await Assert.That(CSharpApiGenerator.DescribeInput(new NuGetPackagesInput([new("Foo", "1.0")], "/cache"))).IsEqualTo("packages:1");
        await Assert.That(CSharpApiGenerator.DescribeInput(new LocalAssembliesInput("net10.0", ["/a.dll", "/b.dll"]))).IsEqualTo("assemblies:2@net10.0");
        await Assert.That(CSharpApiGenerator.DescribeInput(new CustomInput(new EmptySource()))).IsEqualTo("custom-source");
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
            new LocalAssembliesInput("net10.0", ["/x.dll"]),
        ]);
        await Assert.That(label).IsEqualTo("manifest:/r,assemblies:1@net10.0");
    }

    /// <summary>BuildManifestJson emits the expected nuget-packages.json shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildManifestJsonShape()
    {
        var bytes = AssemblySourceFactory.BuildManifestJson(
            new NuGetPackagesInput([new("Foo", "1.2.3"), new("Bar", "4.5")], "/cache"));
        var json = Encoding.UTF8.GetString(bytes);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        await Assert.That(root.GetProperty("nugetPackageOwners").GetArrayLength()).IsEqualTo(0);
        await Assert.That(root.GetProperty("tfmPreference").GetArrayLength()).IsGreaterThan(0);
        var pkgs = root.GetProperty("additionalPackages");
        await Assert.That(pkgs.GetArrayLength()).IsEqualTo(2);
        await Assert.That(pkgs[0].GetProperty("id").GetString()).IsEqualTo("Foo");
        await Assert.That(pkgs[0].GetProperty("version").GetString()).IsEqualTo("1.2.3");
    }

    /// <summary>CreateOne dispatches to the right source per input shape.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateOneCustom()
    {
        var src = new EmptySource();
        var resolved = AssemblySourceFactory.CreateOne(new CustomInput(src), NullLogger.Instance);
        await Assert.That(resolved).IsEqualTo(src);
    }

    /// <summary>CreateOne resolves a local-assemblies input to a LocalAssemblySource.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CreateOneLocalAssemblies()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smkd-cln-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var resolved = AssemblySourceFactory.CreateOne(
                new LocalAssembliesInput("net10.0", []),
                NullLogger.Instance);
            await Assert.That(resolved).IsTypeOf<LocalAssemblySource>();
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>Stub IAssemblySource that yields no groups.</summary>
    private sealed class EmptySource : IAssemblySource
    {
        /// <inheritdoc/>
        public IAsyncEnumerable<AssemblyGroup> DiscoverAsync() => DiscoverAsync(CancellationToken.None);

        /// <inheritdoc/>
        public async IAsyncEnumerable<AssemblyGroup> DiscoverAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield break;
        }
    }
}
