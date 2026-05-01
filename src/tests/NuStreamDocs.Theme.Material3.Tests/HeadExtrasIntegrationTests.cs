// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Icons.FontAwesome;
using NuStreamDocs.Icons.Material;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Verifies icon plugins flow into the Material3 head_extras slot.</summary>
public class HeadExtrasIntegrationTests
{
    /// <summary>Registering Font Awesome and Material Symbols should put both link tags in the rendered head.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IconPluginsAppearInRenderedHead()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseFontAwesome()
            .UseMaterialIcons(static opts => opts with { Style = MaterialIconStyle.SymbolsRounded })
            .UseMaterial3Theme()
            .BuildAsync();

        var html = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "page.html"));
        await Assert.That(html).Contains("fontawesome");
        await Assert.That(html).Contains("Material+Symbols+Rounded");
    }
}
