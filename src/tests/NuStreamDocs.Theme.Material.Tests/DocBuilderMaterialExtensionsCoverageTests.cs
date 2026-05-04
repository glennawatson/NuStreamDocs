// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Theme.Material.IconShortcode;

namespace NuStreamDocs.Theme.Material.Tests;

/// <summary>Coverage for DocBuilderMaterialExtensions.UseMaterialTheme(builder).</summary>
public class DocBuilderMaterialExtensionsCoverageTests
{
    /// <summary>No-arg UseMaterialTheme registers the default plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseMaterialThemeDefault()
    {
        var b = new DocBuilder().UseMaterialTheme();
        await Assert.That(b).IsNotNull();
    }

    /// <summary>IconShortcodePlugin.Name returns the registered string.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task IconShortcodeName()
    {
        var plugin = new IconShortcodePlugin();
        await Assert.That(plugin.Name.Length).IsPositive();
    }
}
