// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.Metadata.Tests;

/// <summary>End-to-end tests for <c>MetadataCollector</c>.</summary>
public class MetadataCollectorTests
{
    /// <summary>A directory <c>_meta.yml</c> applies to every page below.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryFileAppliesToEveryDescendant()
    {
        using var temp = TempTree.Create();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "_meta.yml"), "author: Root\n");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "intro.md"), "# Intro");
        var sub = Path.Combine(temp.Root, "guide");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "advanced.md"), "# Advanced");

        var registry = MetadataCollector.Build(temp.Root, MetadataOptions.Default);

        await Assert.That(BodyOf(registry, "intro.md")).IsEqualTo("author: Root\n");
        await Assert.That(BodyOf(registry, "guide/advanced.md")).IsEqualTo("author: Root\n");
    }

    /// <summary>A nested <c>_meta.yml</c> overrides keys from ancestor files.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClosestDirectoryWins()
    {
        using var temp = TempTree.Create();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "_meta.yml"), "author: Root\nlayout: default\n");
        var sub = Path.Combine(temp.Root, "guide");
        Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "_meta.yml"), "author: Guide\n");
        await File.WriteAllTextAsync(Path.Combine(sub, "intro.md"), "# Intro");

        var registry = MetadataCollector.Build(temp.Root, MetadataOptions.Default);
        var body = BodyOf(registry, "guide/intro.md");

        await Assert.That(body.Contains("author: Guide", StringComparison.Ordinal)).IsTrue();
        await Assert.That(body.Contains("author: Root", StringComparison.Ordinal)).IsFalse();
        await Assert.That(body.Contains("layout: default", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>Per-page sidecar files override directory-level keys.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SidecarOverridesDirectory()
    {
        using var temp = TempTree.Create();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "_meta.yml"), "author: Root\n");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "intro.md"), "# Intro");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "intro.md.meta.yml"), "author: Sidecar\n");

        var registry = MetadataCollector.Build(temp.Root, MetadataOptions.Default);
        var body = BodyOf(registry, "intro.md");

        await Assert.That(body.Contains("author: Sidecar", StringComparison.Ordinal)).IsTrue();
        await Assert.That(body.Contains("author: Root", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>Returns a UTF-8 string view of the registry bytes for <paramref name="relativePath"/>.</summary>
    /// <param name="registry">Built registry.</param>
    /// <param name="relativePath">Page-relative path.</param>
    /// <returns>Decoded bytes.</returns>
    private static string BodyOf(MetadataRegistry registry, string relativePath) =>
        Encoding.UTF8.GetString(registry.ExtraFor(relativePath));
}
