// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Icons.Material.Tests;

/// <summary>End-to-end tests for the Material icons / symbols plugin.</summary>
public class MaterialIconsPluginTests
{
    /// <summary>Default style should resolve to Material Symbols Outlined.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DefaultStyleEmitsSymbolsOutlined()
    {
        var html = WriteHeadExtras(new MaterialIconsPlugin());
        await Assert.That(html).Contains("Material+Symbols+Outlined");
        await Assert.That(html).Contains("preconnect");
    }

    /// <summary>Classic icons should emit the icon endpoint URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ClassicIconsEmitMaterialIcons()
    {
        var plugin = new MaterialIconsPlugin(MaterialIconsOptions.Default with { Style = MaterialIconStyle.Classic });
        var html = WriteHeadExtras(plugin);
        await Assert.That(html).Contains("family=Material+Icons");
    }

    /// <summary>Each Symbols variant should resolve to its own URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task SymbolsVariantsEmitMatchingUrls()
    {
        await Assert.That(WriteHeadExtras(new MaterialIconsPlugin(MaterialIconsOptions.Default with { Style = MaterialIconStyle.SymbolsRounded })))
            .Contains("Material+Symbols+Rounded");
        await Assert.That(WriteHeadExtras(new MaterialIconsPlugin(MaterialIconsOptions.Default with { Style = MaterialIconStyle.SymbolsSharp })))
            .Contains("Material+Symbols+Sharp");
    }

    /// <summary>Disabling preconnect should skip the preconnect link tags.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task DisablingPreconnectOmitsPreconnectLinks()
    {
        var plugin = new MaterialIconsPlugin(MaterialIconsOptions.Default with { Preconnect = false });
        var html = WriteHeadExtras(plugin);
        await Assert.That(html).DoesNotContain("preconnect");
    }

    /// <summary>An override URL should win over the style-derived URL.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task OverrideUrlWins()
    {
        var plugin = new MaterialIconsPlugin(MaterialIconsOptions.Default with { StylesheetUrlOverride = "https://example.test/icons.css" });
        var html = WriteHeadExtras(plugin);
        await Assert.That(html).Contains("https://example.test/icons.css");
        await Assert.That(html).DoesNotContain("googleapis");
    }

    /// <summary>Helper: invoke <c>IHeadExtraProvider.WriteHeadExtra</c> and decode.</summary>
    /// <param name="provider">Provider under test.</param>
    /// <returns>The rendered head-extras HTML.</returns>
    [SuppressMessage("Performance", "CA1859", Justification = "Test deliberately exercises the IHeadExtraProvider contract.")]
    private static string WriteHeadExtras(IHeadExtraProvider provider)
    {
        var writer = new ArrayBufferWriter<byte>();
        provider.WriteHeadExtra(writer);
        return Encoding.UTF8.GetString(writer.WrittenSpan);
    }
}
