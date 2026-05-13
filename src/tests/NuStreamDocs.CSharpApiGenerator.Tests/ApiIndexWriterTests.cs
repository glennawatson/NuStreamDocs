// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>Pins <see cref="ApiIndexWriter.BuildBytes"/> on the rendered shape and the <see cref="ApiIndexWriter.IsInfraDirectory"/> infra-name filter.</summary>
public class ApiIndexWriterTests
{
    /// <summary>An empty namespace list short-circuits to a zero-length payload.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildBytesReturnsEmptyForEmptyNamespaceList()
    {
        var bytes = ApiIndexWriter.BuildBytes([], [], [], null);
        await Assert.That(bytes.Length).IsEqualTo(0);
    }

    /// <summary>The default title is emitted when no title is supplied, and bullets render in caller-supplied order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildBytesEmitsDefaultTitleAndNamespaceBullets()
    {
        byte[][] namespaces =
        [
            "Akavache"u8.ToArray(),
            "ReactiveUI"u8.ToArray(),
            "Splat"u8.ToArray()
        ];

        var contents = Encoding.UTF8.GetString(ApiIndexWriter.BuildBytes(namespaces, [], [], null));

        await Assert.That(contents).StartsWith("# API Reference\n\n");
        await Assert.That(contents).Contains("## Namespaces\n\n");
        await Assert.That(contents).Contains("- [`Akavache`](Akavache/)");
        await Assert.That(contents).Contains("- [`ReactiveUI`](ReactiveUI/)");
        await Assert.That(contents).Contains("- [`Splat`](Splat/)");

        var akavacheIdx = contents.IndexOf("Akavache`", StringComparison.Ordinal);
        var reactiveIdx = contents.IndexOf("ReactiveUI`", StringComparison.Ordinal);
        var splatIdx = contents.IndexOf("Splat`", StringComparison.Ordinal);
        await Assert.That(akavacheIdx).IsLessThan(reactiveIdx);
        await Assert.That(reactiveIdx).IsLessThan(splatIdx);
    }

    /// <summary>Custom title and intro bytes are emitted between the heading and the namespace list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildBytesHonoursCustomTitleAndIntro()
    {
        byte[][] namespaces = ["Foo.Bar"u8.ToArray()];
        var contents =
            Encoding.UTF8.GetString(
                ApiIndexWriter.BuildBytes(
                    namespaces,
                    "Custom Title"u8,
                    "Custom intro paragraph."u8,
                    null));

        await Assert.That(contents).StartsWith("# Custom Title\n\n");
        await Assert.That(contents).Contains("Custom intro paragraph.\n\n");
        await Assert.That(contents).Contains("- [`Foo.Bar`](Foo.Bar/)");
    }

    /// <summary>An <c>Order:</c> value emits a YAML frontmatter block ahead of the heading.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildBytesEmitsOrderFrontmatterBlock()
    {
        byte[][] namespaces = ["Foo"u8.ToArray()];
        var contents = Encoding.UTF8.GetString(ApiIndexWriter.BuildBytes(namespaces, [], [], 2));

        await Assert.That(contents).StartsWith("---\nOrder: 2\n---\n\n# API Reference");
    }

    /// <summary>Calling without an <c>Order:</c> writes no frontmatter.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task BuildBytesWithoutOrderHasNoFrontmatter()
    {
        byte[][] namespaces = ["Foo"u8.ToArray()];
        var contents = Encoding.UTF8.GetString(ApiIndexWriter.BuildBytes(namespaces, [], [], null));

        await Assert.That(contents).DoesNotContain("Order:");
        await Assert.That(contents).StartsWith("# API Reference");
    }

    /// <summary><see cref="ApiIndexWriter.IsInfraDirectory"/> matches the well-known infra folder names and rejects everything else.</summary>
    /// <param name="name">Folder name candidate.</param>
    /// <param name="expected">Expected predicate result.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments("lib", true)]
    [Arguments("refs", true)]
    [Arguments("cache", true)]
    [Arguments("_global", true)]
    [Arguments("Splat", false)]
    [Arguments("ReactiveUI", false)]
    [Arguments("Lib", false)]
    public async Task IsInfraDirectoryMatchesKnownFolderNames(string name, bool expected) =>
        await Assert.That(ApiIndexWriter.IsInfraDirectory(Encoding.UTF8.GetBytes(name))).IsEqualTo(expected);
}
