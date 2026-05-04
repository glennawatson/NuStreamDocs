// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Coverage for Material3ThemePlugin Name + Theme accessors.</summary>
public class Material3ThemePluginCoverageTests
{
    /// <summary>Default ctor and Name accessor.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultCtorAndName()
    {
        var plugin = new Material3ThemePlugin();
        await Assert.That(plugin.Name.AsSpan().SequenceEqual("material3-theme"u8)).IsTrue();
        await Assert.That(plugin.Theme).IsNotNull();
    }
}
