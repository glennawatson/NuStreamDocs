// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Nav;

namespace NuStreamDocs.Config.DocFx.Tests;

/// <summary>Tests for <see cref="NavOptionsDocFxExtensions.FromDocFxTocs"/>.</summary>
public class NavOptionsDocFxExtensionsTests
{
    /// <summary>A populated <c>toc.yml</c> produces a curated list mirroring the YAML order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FromDocFxTocsPopulatesCurated()
    {
        using var fixture = TempTocTree.Create();
        const string Toc = """
                           - name: Home
                             href: index.md
                           - name: Guide
                             href: guide/intro.md
                           - name: Reference
                             href: reference.md
                           """;
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "toc.yml"), Toc);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "index.md"), "# Home");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "guide"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "guide", "intro.md"), "# Intro");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "reference.md"), "# Reference");

        var result = NavOptions.Default.FromDocFxTocs(fixture.Root);

        await Assert.That(result.CuratedEntries.Length).IsEqualTo(3);
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[0].Title)).IsEqualTo("Home");
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[0].Path)).IsEqualTo("index.md");
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[1].Title)).IsEqualTo("Guide");
        await Assert.That(Encoding.UTF8.GetString(result.CuratedEntries[2].Title)).IsEqualTo("Reference");
    }

    /// <summary>Empty / whitespace root directory trips the argument-validation guard.</summary>
    /// <param name="root">Invalid root candidate.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("")]
    [Arguments("   ")]
    public async Task FromDocFxTocsRejectsBlankRoot(string root) =>
        await Assert.That(() => NavOptions.Default.FromDocFxTocs(root)).Throws<ArgumentException>();

    /// <summary>A directory with no <c>toc.yml</c> yields an empty curated list rather than throwing.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FromDocFxTocsMissingTocReturnsEmpty()
    {
        using var fixture = TempTocTree.Create();

        var result = NavOptions.Default.FromDocFxTocs(fixture.Root);

        await Assert.That(result.CuratedEntries.Length).IsEqualTo(0);
    }
}
