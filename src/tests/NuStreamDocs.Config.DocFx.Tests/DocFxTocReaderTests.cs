// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Nav;

namespace NuStreamDocs.Config.DocFx.Tests;

/// <summary>End-to-end tests for the docfx-style toc.yml reader.</summary>
public class DocFxTocReaderTests
{
    /// <summary>Toc File Name used by the test cases.</summary>
    private const string TocFileName = "toc.yml";

    /// <summary>Index File Name used by the test cases.</summary>
    private const string IndexFileName = "index.md";

    /// <summary>Expected Entry Count used by the test cases.</summary>
    private const int ExpectedEntryCount = 2;

    /// <summary>Empty docs root with no toc.yml returns an empty entry array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoRootTocReturnsEmpty()
    {
        using var fixture = TempTocTree.Create();
        var entries = DocFxTocReader.ReadTree(fixture.Root);
        await Assert.That(entries).IsEmpty();
    }

    /// <summary>Flat toc.yml with bare leaf hrefs decodes into leaf entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FlatLeafEntriesDecode()
    {
        using var fixture = TempTocTree.Create();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, TocFileName),
            "- name: Home\n  href: index.md\n- name: Guide\n  href: guide.md\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, IndexFileName), "# Home");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide.md"), "# Guide");

        var entries = DocFxTocReader.ReadTree(fixture.Root);
        await Assert.That(entries.Length).IsEqualTo(ExpectedEntryCount);
        await Assert.That(Encoding.UTF8.GetString(entries[0].Title)).IsEqualTo("Home");
        await Assert.That(Encoding.UTF8.GetString(entries[0].Path)).IsEqualTo(IndexFileName);
        await Assert.That(Encoding.UTF8.GetString(entries[1].Title)).IsEqualTo("Guide");
        await Assert.That(Encoding.UTF8.GetString(entries[1].Path)).IsEqualTo("guide.md");
    }

    /// <summary>A directory ref with a sub-toc.yml folds the sub-toc's entries into the parent section.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubTocFolds()
    {
        using var fixture = TempTocTree.Create();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, TocFileName),
            "- name: Docs\n  href: docs/toc.yml\n");
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, "docs"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, "docs", TocFileName),
            "- name: Intro\n  href: intro.md\n");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "docs", "intro.md"), "# Intro");

        var entries = DocFxTocReader.ReadTree(fixture.Root);
        await Assert.That(entries.Length).IsEqualTo(1);

        var docs = entries[0];
        await Assert.That(Encoding.UTF8.GetString(docs.Title)).IsEqualTo("Docs");
        await Assert.That(docs.IsSection).IsTrue();
        await Assert.That(docs.Children.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(docs.Children[0].Title)).IsEqualTo("Intro");
        await Assert.That(Encoding.UTF8.GetString(docs.Children[0].Path)).IsEqualTo("docs/intro.md");
    }

    /// <summary>A directory href with a homepage attaches the landing page as the section's <see cref="NavEntry.Path"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryHrefWithHomepageAttachesLandingPage()
    {
        using var fixture = TempTocTree.Create();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, TocFileName),
            "- name: Guide\n  href: guide/\n  homepage: guide/index.md\n");
        _ = Directory.CreateDirectory(Path.Combine(fixture.Root, "guide"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide", IndexFileName), "# Guide");

        var entries = DocFxTocReader.ReadTree(fixture.Root);
        await Assert.That(entries.Length).IsEqualTo(1);

        var guide = entries[0];
        await Assert.That(Encoding.UTF8.GetString(guide.Title)).IsEqualTo("Guide");
        await Assert.That(Encoding.UTF8.GetString(guide.Path)).IsEqualTo("guide/index.md");
    }

    /// <summary>An external http href passes through verbatim.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExternalLinkPassesThrough()
    {
        using var fixture = TempTocTree.Create();
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, TocFileName),
            "- name: GitHub\n  href: https://github.com/\n");

        var entries = DocFxTocReader.ReadTree(fixture.Root);
        await Assert.That(entries.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(entries[0].Title)).IsEqualTo("GitHub");
        await Assert.That(Encoding.UTF8.GetString(entries[0].Path)).IsEqualTo("https://github.com/");
    }

    /// <summary>Inline <c>items:</c> sub-list materializes a section without a sub-toc file.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task InlineItemsListBuildsSection()
    {
        using var fixture = TempTocTree.Create();
        const string Yaml =
            "- name: Group\n  items:\n  - name: One\n    href: one.md\n  - name: Two\n    href: two.md\n";
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, TocFileName), Yaml);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "one.md"), "# One");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "two.md"), "# Two");

        var entries = DocFxTocReader.ReadTree(fixture.Root);
        await Assert.That(entries.Length).IsEqualTo(1);

        var group = entries[0];
        await Assert.That(group.IsSection).IsTrue();
        await Assert.That(group.Children.Length).IsEqualTo(ExpectedEntryCount);
        await Assert.That(Encoding.UTF8.GetString(group.Children[0].Path)).IsEqualTo("one.md");
        await Assert.That(Encoding.UTF8.GetString(group.Children[1].Path)).IsEqualTo("two.md");
    }
}
