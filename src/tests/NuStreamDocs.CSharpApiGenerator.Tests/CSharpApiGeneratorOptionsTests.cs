// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Behavior tests for <c>CSharpApiGeneratorOptions</c>.</summary>
public class CSharpApiGeneratorOptionsTests
{
    /// <summary>Repository Directory used by the test cases.</summary>
    private const string RepositoryDirectory = "/repo";

    /// <summary>Cache Directory used by the test cases.</summary>
    private const string CacheDirectory = "/cache";

    /// <summary>Target Framework used by the test cases.</summary>
    private const string TargetFramework = "net10.0";

    /// <summary>Input Count used by the test cases.</summary>
    private const int InputCount = 2;

    /// <summary>The factory uses the documented default subdirectory.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DefaultSubdirectoryIsApi()
    {
        var options = CSharpApiGeneratorOptions.FromManifest(RepositoryDirectory, CacheDirectory);
        await Assert.That(options.OutputMarkdownSubdirectory)
            .IsEqualTo(CSharpApiGeneratorOptions.DefaultOutputSubdirectory);
        await Assert.That(options.OutputMarkdownSubdirectory).IsEqualTo("api");
    }

    /// <summary><c>CSharpApiGeneratorOptions.Validate</c> rejects empty / whitespace-only values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValidateRejectsEmptyFields()
    {
        await Assert.That(static () => CSharpApiGeneratorOptions.FromManifest(string.Empty, CacheDirectory).Validate())
            .Throws<ArgumentException>();
        await Assert.That(static () => CSharpApiGeneratorOptions.FromManifest(RepositoryDirectory, "  ").Validate())
            .Throws<ArgumentException>();
        await Assert.That(static () =>
                CSharpApiGeneratorOptions.FromManifest(RepositoryDirectory, CacheDirectory, string.Empty).Validate())
            .Throws<ArgumentException>();
    }

    /// <summary><c>CSharpApiGeneratorOptions.Validate</c> accepts a fully-populated manifest record.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValidateAcceptsPopulatedManifest()
    {
        var options = CSharpApiGeneratorOptions.FromManifest(RepositoryDirectory, CacheDirectory, "reference");
        options.Validate();
        await Assert.That(options.OutputMarkdownSubdirectory).IsEqualTo("reference");
    }

    /// <summary><c>CSharpApiGeneratorOptions.FromPackages</c> validates and round-trips the package list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FromPackagesRoundTripsList()
    {
        var options = CSharpApiGeneratorOptions.FromPackages(
            [new("ReactiveUI", "20.0.0")],
            CacheDirectory);
        options.Validate();
        await Assert.That(options.Inputs.Length).IsEqualTo(1);
        var input = options.Inputs[0];
        var packagesInput = input as NuGetPackagesInput;
        await Assert.That(packagesInput).IsNotNull();
        await Assert.That(packagesInput!.Packages.Length).IsEqualTo(1);
    }

    /// <summary><c>CSharpApiGeneratorOptions.FromAssemblies</c> validates and round-trips the dll list.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FromAssembliesRoundTripsList()
    {
        var options = CSharpApiGeneratorOptions.FromAssemblies(TargetFramework, ["/tmp/foo.dll"]);
        options.Validate();
        var input = options.Inputs[0] as LocalAssembliesInput;
        await Assert.That(input).IsNotNull();
        await Assert.That(input!.Tfm).IsEqualTo(TargetFramework);
    }

    /// <summary>Composite mode (multiple inputs) validates each entry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FromCompositeValidatesEveryEntry()
    {
        var options = CSharpApiGeneratorOptions.From(
            new NuGetManifestInput(RepositoryDirectory, CacheDirectory),
            new LocalAssembliesInput(TargetFramework, ["/tmp/foo.dll"]));
        options.Validate();
        await Assert.That(options.Inputs.Length).IsEqualTo(InputCount);
    }

    /// <summary>Validate rejects an empty input array.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValidateRejectsEmptyInputs()
    {
        CSharpApiGeneratorOptions options = new([], "api", CSharpApiGeneratorMode.EmitMarkdown);
        await Assert.That(options.Validate).Throws<ArgumentException>();
    }
}
