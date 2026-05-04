// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.CSharpApiGenerator.Tests;

/// <summary>End-to-end tests for the API landing-page <c>index.md</c> emitter.</summary>
public class ApiIndexWriterTests
{
    /// <summary>A missing API root short-circuits with zero namespaces written.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsZeroWhenApiPathDoesNotExist()
    {
        var apiPath = (DirectoryPath)Path.Combine(Path.GetTempPath(), "smkd-apidx-missing-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        var written = ApiIndexWriter.Write(apiPath, [], []);
        await Assert.That(written).IsEqualTo(0);
    }

    /// <summary>A folder with only infra dirs (lib/refs/cache/_global) yields no index file.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ReturnsZeroWhenOnlyInfraDirectoriesPresent()
    {
        using var fixture = new TempApiTree();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "lib"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "refs"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "cache"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "_global"));

        var written = ApiIndexWriter.Write((DirectoryPath)fixture.Root, [], []);

        await Assert.That(written).IsEqualTo(0);
        await Assert.That(File.Exists(Path.Combine(fixture.Root, "index.md"))).IsFalse();
    }

    /// <summary>Real namespaces produce a sorted bullet list, infra dirs are skipped, and the default title is used when none is supplied.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmitsSortedNamespaceListWithDefaultTitle()
    {
        using var fixture = new TempApiTree();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "Akavache"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "ReactiveUI"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "Splat"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "lib"));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "_global"));

        var written = ApiIndexWriter.Write((DirectoryPath)fixture.Root, [], []);
        await Assert.That(written).IsEqualTo(3);

        var indexPath = Path.Combine(fixture.Root, "index.md");
        await Assert.That(File.Exists(indexPath)).IsTrue();
        var contents = await File.ReadAllTextAsync(indexPath, Encoding.UTF8);

        await Assert.That(contents).StartsWith("# API Reference\n\n");
        await Assert.That(contents).Contains("## Namespaces\n\n");
        await Assert.That(contents).Contains("- [`Akavache`](Akavache/)");
        await Assert.That(contents).Contains("- [`ReactiveUI`](ReactiveUI/)");
        await Assert.That(contents).Contains("- [`Splat`](Splat/)");
        await Assert.That(contents).DoesNotContain("lib");
        await Assert.That(contents).DoesNotContain("_global");

        var akavacheIdx = contents.IndexOf("Akavache`", StringComparison.Ordinal);
        var reactiveIdx = contents.IndexOf("ReactiveUI`", StringComparison.Ordinal);
        var splatIdx = contents.IndexOf("Splat`", StringComparison.Ordinal);
        await Assert.That(akavacheIdx).IsLessThan(reactiveIdx);
        await Assert.That(reactiveIdx).IsLessThan(splatIdx);
    }

    /// <summary>Custom title and intro bytes are emitted between the heading and the namespace list.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HonoursCustomTitleAndIntro()
    {
        using var fixture = new TempApiTree();
        Directory.CreateDirectory(Path.Combine(fixture.Root, "Foo.Bar"));

        var written = ApiIndexWriter.Write(
            (DirectoryPath)fixture.Root,
            "Custom Title"u8,
            "Custom intro paragraph."u8);

        await Assert.That(written).IsEqualTo(1);
        var contents = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "index.md"), Encoding.UTF8);
        await Assert.That(contents).StartsWith("# Custom Title\n\n");
        await Assert.That(contents).Contains("Custom intro paragraph.\n\n");
        await Assert.That(contents).Contains("- [`Foo.Bar`](Foo.Bar/)");
    }

    /// <summary>Disposable temp directory used as the API root for these tests.</summary>
    private sealed class TempApiTree : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempApiTree"/> class.</summary>
        public TempApiTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-apidx-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the temp directory.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch (IOException)
            {
                // best-effort cleanup
            }
        }
    }
}
