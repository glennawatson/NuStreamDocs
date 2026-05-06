// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;

namespace NuStreamDocs.Theme.Material3.Tests;

/// <summary>Verifies <c>hide: [toc]</c> frontmatter hides the secondary sidebar.</summary>
public class HideTocFrontmatterTests
{
    /// <summary>Pages with <c>hide: [toc]</c> render with <c>hidden</c> on the secondary sidebar.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task HideTocFrontmatterAddsHiddenToSecondarySidebar()
    {
        using var fixture = TempBuildTree.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "withtoc.md"), "# Plain\n");
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Docs, "notoc.md"),
            "---\nhide:\n  - toc\n---\n# No Toc\n");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseMaterial3Theme()
            .BuildAsync();

        var hidden = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "notoc.html"));
        var visible = await File.ReadAllTextAsync(Path.Combine(fixture.Site, "withtoc.html"));

        await Assert.That(hidden).Contains("md-sidebar--secondary").And.Contains("data-md-type=\"toc\" hidden");
        await Assert.That(visible).Contains("md-sidebar--secondary").And.DoesNotContain("data-md-type=\"toc\" hidden");
    }
}
