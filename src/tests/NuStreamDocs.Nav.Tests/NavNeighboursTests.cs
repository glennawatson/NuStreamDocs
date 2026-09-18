// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Nav.Tests;

/// <summary>Tests for the <c>INavNeighboursProvider</c> implementation on <c>NavPlugin</c>.</summary>
public class NavNeighboursTests
{
    /// <summary>The AlphaDirectory fixture value.</summary>
    private const string AlphaDirectory = "alpha";

    /// <summary>The GuideDirectory fixture value.</summary>
    private const string GuideDirectory = "guide";

    /// <summary>Across two sibling sections, GetNeighbours crosses section boundaries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetNeighboursCrossesSectionBoundary()
    {
        using var fixture = TempDocsTree.Create();
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, AlphaDirectory));
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, "beta"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, AlphaDirectory, "a.md"), "a");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, AlphaDirectory, "b.md"), "b");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "beta", "c.md"), "c");

        NavPlugin plugin = new(NavOptions.Default with { Indexes = false });
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        var neighbours = plugin.GetNeighbours("alpha/b.md");
        await Assert.That(neighbours.PreviousPath).IsEqualTo("alpha/a.md");
        await Assert.That(neighbours.NextPath).IsEqualTo("beta/c.md");
    }

    /// <summary>GetSectionNeighbours stops at section boundaries.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task GetSectionNeighboursStaysWithinSection()
    {
        using var fixture = TempDocsTree.Create();
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, AlphaDirectory));
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, "beta"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, AlphaDirectory, "a.md"), "a");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, AlphaDirectory, "b.md"), "b");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "beta", "c.md"), "c");

        NavPlugin plugin = new(NavOptions.Default with { Indexes = false });
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        var neighbours = plugin.GetSectionNeighbours("alpha/b.md");
        await Assert.That(neighbours.PreviousPath).IsEqualTo("alpha/a.md");
        await Assert.That(neighbours.NextPath.IsEmpty).IsTrue();
    }

    /// <summary>An unknown path returns <c>NavNeighbours.None</c>.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task UnknownPathReturnsNone()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "only.md"), "only");

        NavPlugin plugin = new();
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        var neighbours = plugin.GetNeighbours("not-in-tree.md");
        await Assert.That(neighbours).IsEqualTo(NavNeighbours.None);
    }

    /// <summary>Navigation.indexes promotes <c>index.md</c> so the section's landing leaf appears in the linear order.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PromotedIndexAppearsInLinearOrder()
    {
        using var fixture = TempDocsTree.Create();
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, GuideDirectory));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, GuideDirectory, "index.md"), "intro");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, GuideDirectory, "next.md"), "next");

        NavPlugin plugin = new(NavOptions.Default with { Indexes = true });
        await new DocBuilder().WithInput(fixture.Root).WithOutput(fixture.Output).UsePlugin(plugin).BuildAsync();

        var neighbours = plugin.GetNeighbours("guide/next.md");
        await Assert.That(neighbours.PreviousPath).IsEqualTo("guide/index.md");
    }
}
