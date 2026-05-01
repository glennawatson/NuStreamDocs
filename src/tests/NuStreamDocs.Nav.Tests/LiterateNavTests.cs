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
        await Assert.That(section.Title).IsEqualTo("User Guide");
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
        await Assert.That(Array.Exists(root.Children, static c => c.IsSection && c.Title == "secret")).IsFalse();
    }
}
