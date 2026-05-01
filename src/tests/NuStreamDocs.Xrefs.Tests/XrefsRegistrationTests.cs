// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Autorefs;
using NuStreamDocs.Building;

namespace NuStreamDocs.Xrefs.Tests;

/// <summary>Builder-extension + options tests for <c>XrefsPlugin</c>.</summary>
public class XrefsRegistrationTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new XrefsPlugin().Name).IsEqualTo("xrefs");

    /// <summary>Default options has the expected fields.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultOptions()
    {
        await Assert.That(XrefsOptions.Default.OutputFileName).IsEqualTo("xrefmap.json");
        await Assert.That(XrefsOptions.Default.EmitMap).IsTrue();
        await Assert.That(XrefsOptions.Default.Imports.Length).IsEqualTo(0);
    }

    /// <summary>Validate() throws on empty OutputFileName.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ValidateThrowsOnEmpty()
    {
        var bad = XrefsOptions.Default with { OutputFileName = string.Empty };
        var ex = Assert.Throws<ArgumentException>(bad.Validate);
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseXrefs() registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseXrefsRegisters() =>
        await Assert.That(new DocBuilder().UseXrefs()).IsTypeOf<DocBuilder>();

    /// <summary>UseXrefs(options) registers.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseXrefsOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseXrefs(XrefsOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseXrefs(registry, options) registers with a shared registry.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseXrefsWithRegistryRegisters() =>
        await Assert.That(new DocBuilder().UseXrefs(new AutorefsRegistry(), XrefsOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseXrefs rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseXrefsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderXrefsExtensions.UseXrefs(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseXrefs(options) rejects null options.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseXrefsOptionsRejectsNullOptions()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => new DocBuilder().UseXrefs((XrefsOptions)null!));
        await Assert.That(ex).IsNotNull();
    }
}
