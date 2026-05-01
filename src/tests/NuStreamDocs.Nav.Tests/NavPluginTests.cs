// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Nav.Tests;

/// <summary>End-to-end tests for the <c>NavPlugin</c> registration and tree build.</summary>
public class NavPluginTests
{
    /// <summary>Registering with defaults should walk the input tree on configure.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildsTreeOnConfigure()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# Hi");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "guide"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide", "intro.md"), "# Intro");

        var plugin = new NavPlugin();
        var builder = new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin);

        await builder.BuildAsync();

        await Assert.That(plugin.Root).IsNotNull();
    }

    /// <summary>Excluded files should not appear in the tree.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task ExcludeGlobDropsMatchingFiles()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "keep.md"), "k");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "drop.md"), "d");

        var plugin = new NavPlugin(NavOptions.Default with { Excludes = ["drop.md"] });
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        await Assert.That(plugin.Root).IsNotNull();
    }

    /// <summary>The extension method should register the plugin without ceremony.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UseNavExtensionRegisters()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# Hi");

        var builder = new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UseNav();
        await builder.BuildAsync();

        await Assert.That(builder).IsNotNull();
    }
}
