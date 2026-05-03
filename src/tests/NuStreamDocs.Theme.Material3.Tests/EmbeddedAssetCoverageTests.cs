// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Coverage for Material3 EmbeddedAsset.ToResourceName.</summary>
public class EmbeddedAssetCoverageTests
{
    /// <summary>ToResourceName produces a non-empty resource path.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ToResourceNameProducesPath()
    {
        var name = EmbeddedAsset.ToResourceName("css/site.css");
        await Assert.That(name).IsNotNull();
        await Assert.That(name.Length).IsGreaterThan(0);
    }

    /// <summary>Vendored package paths map to the embedded resource names MSBuild emits.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task VendoredPathUsesMsbuildResourceShape()
    {
        var name = EmbeddedAsset.ToResourceName("assets/vendor/@lit/reactive-element/css-tag.js");
        await Assert.That(name).IsEqualTo("NuStreamDocs.Theme.Material3.Templates.assets.vendor._lit.reactive_element.css-tag.js");
    }

    /// <summary>Vendored official runtime assets can be read from the embedded-resource table.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task VendoredRuntimeAssetCanBeRead()
    {
        var bytes = EmbeddedAsset.ReadBytes("assets/vendor/@lit/reactive-element/css-tag.js");
        await Assert.That(bytes.Length).IsGreaterThan(0);
    }
}
