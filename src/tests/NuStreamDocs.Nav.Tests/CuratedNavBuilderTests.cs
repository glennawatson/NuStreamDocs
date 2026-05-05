// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav.Tests;

/// <summary>End-to-end tests for the curated-nav builder integration with <c>NavPlugin</c>.</summary>
public class CuratedNavBuilderTests
{
    /// <summary>Curated entries fed via <see cref="NavOptions.CuratedEntries"/> drive the rendered tree instead of the directory walk.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CuratedEntriesDriveRenderedTree()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# Home");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "extra.md"), "# Extra");

        NavEntry[] entries =
        [
            NavEntryFactory.Leaf("Home", "index.md"),
            NavEntryFactory.Leaf("Guide", "guide.md")
        ];
        var options = NavOptions.Default.WithCuratedEntries(entries);
        NavPlugin plugin = new(options);

        await new Building.DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)
            .UsePlugin(plugin)
            .BuildAsync();

        var root = (NavNode)plugin.Root!;
        await Assert.That(root.Children.Length).IsEqualTo(2);
        await Assert.That(System.Text.Encoding.UTF8.GetString(root.Children[0].Title)).IsEqualTo("Home");
        await Assert.That(System.Text.Encoding.UTF8.GetString(root.Children[1].Title)).IsEqualTo("Guide");
    }

    /// <summary>An empty curated list falls back to the auto-discovery walker.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyCuratedListFallsBackToAutoDiscovery()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# Home");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide.md"), "# Guide");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "extra.md"), "# Extra");

        NavPlugin plugin = new(NavOptions.Default);
        await new Building.DocBuilder()
            .WithInput(fixture.Root)
            .WithOutput(fixture.Output)
            .UsePlugin(plugin)
            .BuildAsync();

        var root = (NavNode)plugin.Root!;

        // Auto-discovery picks up every top-level .md file (3 here).
        await Assert.That(root.Children.Length).IsEqualTo(3);
    }

    /// <summary>Curated entries honor directory-style served URLs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CuratedEntriesHonorDirectoryUrls()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide.md"), "# Guide");

        var root = CuratedNavBuilder.Build(
            fixture.Root,
            [NavEntryFactory.Leaf("Guide", "guide.md")],
            useDirectoryUrls: true);

        await Assert.That(System.Text.Encoding.UTF8.GetString(root.Children[0].RelativeUrlBytes)).IsEqualTo("guide/");
    }
}
