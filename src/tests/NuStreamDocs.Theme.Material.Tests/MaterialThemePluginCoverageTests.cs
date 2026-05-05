// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>Coverage for MaterialThemePlugin Name + default ctor + Theme accessor.</summary>
public class MaterialThemePluginCoverageTests
{
    /// <summary>Default ctor and Name accessor.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultCtorAndName()
    {
        MaterialThemePlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("material-theme"u8)).IsTrue();
        await Assert.That(plugin.Theme).IsNotNull();
    }
}
