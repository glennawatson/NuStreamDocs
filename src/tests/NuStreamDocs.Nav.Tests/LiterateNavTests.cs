// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Nav.Tests;

/// <summary>End-to-end tests for literate-nav (<c>.pages</c>) overrides applied during <c>NavTreeBuilder.Build(string, in NavOptions)</c>.</summary>
public class LiterateNavTests
{
    /// <summary><c>nav:</c> entries reorder children to match the explicit list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NavOverrideReordersChildren()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "alpha.md"), "# alpha");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "beta.md"), "# beta");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "gamma.md"), "# gamma");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, ".pages"), "nav:\n  - gamma.md\n  - alpha.md\n");

        var root = NavTreeBuilder.Build(fixture.Root, NavOptions.Default);
        var names = new string[root.Children.Length];
        for (var i = 0; i < root.Children.Length; i++)
        {
            names[i] = Path.GetFileNameWithoutExtension(root.Children[i].RelativePath);
        }

        var ordered = string.Join('|', names);

        await Assert.That(ordered).IsEqualTo("gamma|alpha|beta");
    }

    /// <summary><c>title:</c> overrides the section's display title.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TitleOverrideTakesEffect()
    {
        using var fixture = TempDocsTree.Create();
        var sub = Path.Combine(fixture.Root, "guide");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "intro.md"), "# intro");
        await File.WriteAllTextAsync(Path.Combine(sub, ".pages"), "title: User Guide\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# home");

        var root = NavTreeBuilder.Build(fixture.Root, NavOptions.Default);
        var section = Array.Find(root.Children, static c => c.IsSection)!;
        await Assert.That(System.Text.Encoding.UTF8.GetString(section.Title)).IsEqualTo("User Guide");
    }

    /// <summary>Awesome-pages <c>- Title: path</c> entries both reorder children and override the matched section's display title.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AwesomePagesTitleMapAppliesOverride()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# home");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "alpha.md"), "# Alpha");
        var sub = Path.Combine(fixture.Root, "client");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "page.md"), "# Inner");
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, ".pages"),
            "nav:\n  - Client Usage: client\n  - alpha.md\n");

        var root = NavTreeBuilder.Build(fixture.Root, NavOptions.Default);

        var section = Array.Find(root.Children, static c => c.IsSection)!;
        await Assert.That(System.Text.Encoding.UTF8.GetString(section.Title)).IsEqualTo("Client Usage");

        // The matched section comes first because the .pages ordering puts client before alpha.md.
        var clientIdx = Array.IndexOf(root.Children, section);
        var alphaIdx = Array.FindIndex(root.Children, static c => !c.IsSection && Path.GetFileNameWithoutExtension(c.RelativePath) == "alpha");
        await Assert.That(clientIdx < alphaIdx).IsTrue();

        // Bare entries (no Title:) keep their natural display title.
        var alpha = root.Children[alphaIdx];
        await Assert.That(System.Text.Encoding.UTF8.GetString(alpha.Title)).IsEqualTo("Alpha");
    }

    /// <summary><c>hide: true</c> drops the section from its parent's children.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HideTrueDropsSection()
    {
        using var fixture = TempDocsTree.Create();
        var sub = Path.Combine(fixture.Root, "secret");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "page.md"), "# secret");
        await File.WriteAllTextAsync(Path.Combine(sub, ".pages"), "hide: true\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# home");

        var root = NavTreeBuilder.Build(fixture.Root, NavOptions.Default);
        await Assert.That(Array.Exists(root.Children, static c => c.IsSection && System.Text.Encoding.UTF8.GetString(c.Title) == "secret")).IsFalse();
    }

    /// <summary><c>order: desc</c> reverses a section's default (filename) child ordering — newest-first for a date-prefixed section.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OrderDescReversesChildren()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "2013-01-01-old.md"), "# old");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "2020-06-15-mid.md"), "# mid");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "2026-05-07-new.md"), "# new");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, ".pages"), "order: desc\n");

        var root = NavTreeBuilder.Build(fixture.Root, NavOptions.Default);
        var names = new string[root.Children.Length];
        for (var i = 0; i < root.Children.Length; i++)
        {
            names[i] = Path.GetFileNameWithoutExtension(root.Children[i].RelativePath);
        }

        await Assert.That(string.Join('|', names)).IsEqualTo("2026-05-07-new|2020-06-15-mid|2013-01-01-old");
    }

    /// <summary>An explicit <c>nav:</c> list still wins over <c>order: desc</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NavListBeatsOrderDesc()
    {
        using var fixture = TempDocsTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "a.md"), "# a");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "b.md"), "# b");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "c.md"), "# c");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, ".pages"), "order: desc\nnav:\n  - b.md\n  - a.md\n");

        var root = NavTreeBuilder.Build(fixture.Root, NavOptions.Default);
        var names = new string[root.Children.Length];
        for (var i = 0; i < root.Children.Length; i++)
        {
            names[i] = Path.GetFileNameWithoutExtension(root.Children[i].RelativePath);
        }

        await Assert.That(string.Join('|', names)).IsEqualTo("b|a|c");
    }
}
