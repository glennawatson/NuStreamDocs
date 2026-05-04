// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Metadata.Tests;

/// <summary>Builder-extension + options tests for <c>MetadataPlugin</c>.</summary>
public class MetadataRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new MetadataPlugin().Name.AsSpan().SequenceEqual("metadata"u8)).IsTrue();

    /// <summary>Default options exposes the expected file names.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultOptions()
    {
        await Assert.That(MetadataOptions.Default.DirectoryFileName).IsEqualTo("_meta.yml");
        await Assert.That(MetadataOptions.Default.SidecarSuffix).IsEqualTo(".meta.yml");
    }

    /// <summary>Validate() throws on empty fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnEmpty()
    {
        Assert.Throws<ArgumentException>(static () => new MetadataOptions(string.Empty, ".meta.yml").Validate());
        var ex = Assert.Throws<ArgumentException>(static () => new MetadataOptions("_meta.yml", string.Empty).Validate());
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseMetadata() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMetadataRegisters() =>
        await Assert.That(new DocBuilder().UseMetadata()).IsTypeOf<DocBuilder>();

    /// <summary>UseMetadata(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMetadataOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseMetadata(MetadataOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseMetadata rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMetadataRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderMetadataExtensions.UseMetadata(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseMetadata(options) rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMetadataRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseMetadata(null!));
        await Assert.That(ex).IsNotNull();
    }
}
