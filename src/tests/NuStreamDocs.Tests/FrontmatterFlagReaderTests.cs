// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Yaml;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>FrontmatterFlagReader</c>.</summary>
public class FrontmatterFlagReaderTests
{
    /// <summary><c>draft: true</c> sets the Draft flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DraftTrueSetsFlag() =>
        await Assert.That(Read("---\ndraft: true\n---\n")).IsEqualTo(PageFlags.Draft);

    /// <summary><c>draft: yes</c> also sets the Draft flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DraftYesSetsFlag() =>
        await Assert.That(Read("---\ndraft: yes\n---\n")).IsEqualTo(PageFlags.Draft);

    /// <summary><c>not_in_nav: true</c> sets the NotInNav flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NotInNavSetsFlag() =>
        await Assert.That(Read("---\nnot_in_nav: true\n---\n")).IsEqualTo(PageFlags.NotInNav);

    /// <summary><c>nav_exclude: true</c> is an accepted alias for <c>not_in_nav</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NavExcludeAliasSetsFlag() =>
        await Assert.That(Read("---\nnav_exclude: true\n---\n")).IsEqualTo(PageFlags.NotInNav);

    /// <summary>Both flags can co-exist.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BothFlagsCombine() =>
        await Assert.That(Read("---\ndraft: true\nnot_in_nav: true\n---\n"))
            .IsEqualTo(PageFlags.Draft | PageFlags.NotInNav);

    /// <summary>A non-truthy value yields no flag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FalseValueYieldsNothing() =>
        await Assert.That(Read("---\ndraft: false\nnot_in_nav: no\n---\n")).IsEqualTo(PageFlags.None);

    /// <summary>Source with no frontmatter yields no flags.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoFrontmatterIsNone() =>
        await Assert.That(Read("just body, no frontmatter")).IsEqualTo(PageFlags.None);

    /// <summary>Empty source yields no flags.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptySourceIsNone() =>
        await Assert.That(FrontmatterFlagReader.ReadFlags([])).IsEqualTo(PageFlags.None);

    /// <summary>Read(path) reads from disk and returns the same flags.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadPathRoundtrips()
    {
        var path = Path.Combine(Path.GetTempPath(), "smkd-flag-" + Guid.NewGuid().ToString("N") + ".md");
        try
        {
            await File.WriteAllTextAsync(path, "---\ndraft: true\n---\nbody");
            await Assert.That(FrontmatterFlagReader.Read(path)).IsEqualTo(PageFlags.Draft);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Read(path) returns None when the file is missing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadMissingPathIsNone() =>
        await Assert.That(FrontmatterFlagReader.Read("/nonexistent/" + Guid.NewGuid().ToString("N") + ".md"))
            .IsEqualTo(PageFlags.None);

    /// <summary>Read(path) rejects null/empty.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReadPathRejectsEmpty()
    {
        var ex = Assert.Throws<ArgumentException>(static () => FrontmatterFlagReader.Read(string.Empty));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>A frontmatter with only the opening delimiter and no closing yields no flags.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UnclosedFrontmatterIsNone() =>
        await Assert.That(Read("---only opener")).IsEqualTo(PageFlags.None);

    /// <summary>Encodes <paramref name="source"/> as UTF-8 and reads the flags.</summary>
    /// <param name="source">Markdown source.</param>
    /// <returns>Detected flags.</returns>
    private static PageFlags Read(string source) =>
        FrontmatterFlagReader.ReadFlags(Encoding.UTF8.GetBytes(source));
}
