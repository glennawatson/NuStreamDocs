// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Behavior tests for <c>CSharpApiGeneratorOptions</c>.</summary>
public class CSharpApiGeneratorOptionsTests
{
    /// <summary>The factory uses the documented default subdirectory.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DefaultSubdirectoryIsApi()
    {
        var options = CSharpApiGeneratorOptions.FromManifest("/repo", "/cache");
        await Assert.That(options.OutputMarkdownSubdirectory).IsEqualTo(CSharpApiGeneratorOptions.DefaultOutputSubdirectory);
        await Assert.That(options.OutputMarkdownSubdirectory).IsEqualTo("api");
    }

    /// <summary><c>CSharpApiGeneratorOptions.Validate</c> rejects empty / whitespace-only values.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValidateRejectsEmptyFields()
    {
        await Assert.That(static () => CSharpApiGeneratorOptions.FromManifest(string.Empty, "/cache").Validate())
            .Throws<ArgumentException>();
        await Assert.That(static () => CSharpApiGeneratorOptions.FromManifest("/repo", "  ").Validate())
            .Throws<ArgumentException>();
        await Assert.That(static () => CSharpApiGeneratorOptions.FromManifest("/repo", "/cache", string.Empty).Validate())
            .Throws<ArgumentException>();
    }

    /// <summary><c>CSharpApiGeneratorOptions.Validate</c> accepts a fully-populated manifest record.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ValidateAcceptsPopulatedManifest()
    {
        var options = CSharpApiGeneratorOptions.FromManifest("/repo", "/cache", "reference");
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
            "/cache");
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
        var options = CSharpApiGeneratorOptions.FromAssemblies("net10.0", ["/tmp/foo.dll"]);
        options.Validate();
        var input = options.Inputs[0] as LocalAssembliesInput;
        await Assert.That(input).IsNotNull();
        await Assert.That(input!.Tfm).IsEqualTo("net10.0");
    }

    /// <summary>Composite mode (multiple inputs) validates each entry.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FromCompositeValidatesEveryEntry()
    {
        var options = CSharpApiGeneratorOptions.From(
            new NuGetManifestInput("/repo", "/cache"),
            new LocalAssembliesInput("net10.0", ["/tmp/foo.dll"]));
        options.Validate();
        await Assert.That(options.Inputs.Length).IsEqualTo(2);
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
