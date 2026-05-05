// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Theme.Material3.IconShortcode;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Coverage for Material3 IconShortcodePlugin.Name.</summary>
public class Material3IconShortcodePluginCoverageTests
{
    /// <summary>Plugin reports a non-null name.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IconShortcodeName()
    {
        IconShortcodePlugin plugin = new();
        await Assert.That(plugin.Name.Length).IsPositive();
    }
}
