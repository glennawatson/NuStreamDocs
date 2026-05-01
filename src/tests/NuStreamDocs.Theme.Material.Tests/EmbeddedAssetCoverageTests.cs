// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>Coverage for EmbeddedAsset.ToResourceName.</summary>
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
}
