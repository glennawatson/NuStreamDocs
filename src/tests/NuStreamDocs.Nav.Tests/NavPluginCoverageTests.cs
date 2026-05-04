// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav.Tests;

/// <summary>Coverage for NavPlugin.Name and GetRoot before configure.</summary>
public class NavPluginCoverageTests
{
    /// <summary>Name returns "nav".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        var plugin = new NavPlugin();
        await Assert.That(plugin.Name.SequenceEqual("nav"u8)).IsTrue();
    }

    /// <summary>GetRoot is null before OnConfigureAsync runs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task GetRootBeforeConfigure()
    {
        var plugin = new NavPlugin();
        await Assert.That(plugin.GetRoot()).IsNull();
    }
}
