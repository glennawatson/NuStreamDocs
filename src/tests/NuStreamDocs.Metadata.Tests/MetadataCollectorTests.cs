// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;

namespace NuStreamDocs.Metadata.Tests;

/// <summary>End-to-end tests for <c>MetadataCollector</c>.</summary>
public class MetadataCollectorTests
{
    /// <summary>Directory metadata filename.</summary>
    private const string MetadataFileName = "_meta.yml";

    /// <summary>Inherited author declaration.</summary>
    private const string RootAuthor = "author: Root\n";

    /// <summary>Page filename used at either directory level.</summary>
    private const string IntroFileName = "intro.md";

    /// <summary>Page heading for metadata inheritance scenarios.</summary>
    private const string IntroHeading = "# Intro";

    /// <summary>A directory <c>_meta.yml</c> applies to every page below.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DirectoryFileAppliesToEveryDescendant()
    {
        using var temp = TempTree.Create();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, MetadataFileName), RootAuthor);
        await File.WriteAllTextAsync(Path.Combine(temp.Root, IntroFileName), IntroHeading);
        var sub = Path.Combine(temp.Root, "guide");
        _ = Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, "advanced.md"), "# Advanced");

        var registry = MetadataCollector.Build(temp.Root, MetadataOptions.Default);

        await Assert.That(BodyOf(registry, IntroFileName)).IsEqualTo(RootAuthor);
        await Assert.That(BodyOf(registry, "guide/advanced.md")).IsEqualTo(RootAuthor);
    }

    /// <summary>A nested <c>_meta.yml</c> overrides keys from ancestor files.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ClosestDirectoryWins()
    {
        using var temp = TempTree.Create();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, MetadataFileName), "author: Root\nlayout: default\n");
        var sub = Path.Combine(temp.Root, "guide");
        _ = Directory.CreateDirectory(sub);
        await File.WriteAllTextAsync(Path.Combine(sub, MetadataFileName), "author: Guide\n");
        await File.WriteAllTextAsync(Path.Combine(sub, IntroFileName), IntroHeading);

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
        await File.WriteAllTextAsync(Path.Combine(temp.Root, MetadataFileName), RootAuthor);
        await File.WriteAllTextAsync(Path.Combine(temp.Root, IntroFileName), IntroHeading);
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "intro.md.meta.yml"), "author: Sidecar\n");

        var registry = MetadataCollector.Build(temp.Root, MetadataOptions.Default);
        var body = BodyOf(registry, IntroFileName);

        await Assert.That(body.Contains("author: Sidecar", StringComparison.Ordinal)).IsTrue();
        await Assert.That(body.Contains("author: Root", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>Returns a UTF-8 string view of the registry bytes for <paramref name="relativePath"/>.</summary>
    /// <param name="registry">Built registry.</param>
    /// <param name="relativePath">Page-relative path.</param>
    /// <returns>Decoded bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string BodyOf(MetadataRegistry registry, string relativePath) =>
        Encoding.UTF8.GetString(registry.ExtraFor(relativePath));
}
