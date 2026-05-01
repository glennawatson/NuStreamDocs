// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Icons.Material.Tests;

/// <summary>Coverage for MaterialIconsPlugin.Name.</summary>
public class MaterialIconsPluginCoverageTests
{
    /// <summary>Name returns "material-icons".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        var plugin = new MaterialIconsPlugin();
        await Assert.That(plugin.Name).IsEqualTo("material-icons");
    }
}
