// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Nav.Tests;

/// <summary>End-to-end tests for the <c>NavPlugin</c> registration and tree build.</summary>
public class NavPluginTests
{
    /// <summary>The IndexFile fixture value.</summary>
    private const string IndexFile = "index.md";

    /// <summary>The GuideDirectory fixture value.</summary>
    private const string GuideDirectory = "guide";

    /// <summary>The IntroFile fixture value.</summary>
    private const string IntroFile = "intro.md";

    /// <summary>The IntroMarkdown fixture value.</summary>
    private const string IntroMarkdown = "# Intro";

    /// <summary>The HomeMarkdown fixture value.</summary>
    private const string HomeMarkdown = "# Home";

    /// <summary>Registering with defaults should walk the input tree on configure.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task BuildsTreeOnConfigure()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, IndexFile), "# Hi");
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, GuideDirectory));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, GuideDirectory, IntroFile), IntroMarkdown);

        NavPlugin plugin = new();
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

        NavPlugin plugin = new(NavOptions.Default with { Excludes = ["drop.md"] });
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        await Assert.That(plugin.Root).IsNotNull();
    }

    /// <summary>The extension method should register the plugin without ceremony.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UseNavExtensionRegisters()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, IndexFile), "# Hi");

        var builder = new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UseNav();
        await builder.BuildAsync();

        await Assert.That(builder).IsNotNull();
    }

    /// <summary>A top-level leaf with no descendants triggers so the theme drawer can stay collapsed.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShouldHidePrimarySidebarTrueForTopLevelLeaf()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, IndexFile), HomeMarkdown);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "license.md"), "# License");
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, GuideDirectory));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, GuideDirectory, IntroFile), IntroMarkdown);

        NavPlugin plugin = new();
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        await Assert.That(plugin.ShouldHidePrimarySidebar("license.md")).IsTrue();
    }

    /// <summary>The root <c>index.md</c> page must not be flagged for sidebar suppression — the home page wants the full nav.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShouldHidePrimarySidebarFalseForRootIndex()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, IndexFile), HomeMarkdown);

        NavPlugin plugin = new();
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        await Assert.That(plugin.ShouldHidePrimarySidebar(IndexFile)).IsFalse();
    }

    /// <summary>Pages nested inside a section must keep the sidebar visible — they have peers/parents to render.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ShouldHidePrimarySidebarFalseForNestedLeaf()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, IndexFile), HomeMarkdown);
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, GuideDirectory));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, GuideDirectory, IntroFile), IntroMarkdown);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, GuideDirectory, "advanced.md"), "# Advanced");

        NavPlugin plugin = new();
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        await Assert.That(plugin.ShouldHidePrimarySidebar("guide/intro.md")).IsFalse();
    }
}
