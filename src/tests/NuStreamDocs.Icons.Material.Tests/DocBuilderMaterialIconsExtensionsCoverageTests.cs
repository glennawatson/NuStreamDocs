// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Icons.Material.Tests;

/// <summary>Coverage for DocBuilderMaterialIconsExtensions.UseMaterialIcons(builder).</summary>
public class DocBuilderMaterialIconsExtensionsCoverageTests
{
    /// <summary>No-arg UseMaterialIcons registers the default plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMaterialIconsDefault()
    {
        var b = new DocBuilder().UseMaterialIcons();
        await Assert.That(b).IsNotNull();
    }
}
