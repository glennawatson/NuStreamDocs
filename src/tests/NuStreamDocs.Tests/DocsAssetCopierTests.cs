// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Building;
using NuStreamDocs.Common;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>DocsAssetCopier</c>.</summary>
public class DocsAssetCopierTests
{
    /// <summary>Non-markdown content under the docs root is mirrored into the output tree.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NonMarkdownFilesAreCopiedToOutput()
    {
        using var fixture = ScratchTree.Create();
        await fixture.WriteAsync("images/logo.png", "binary-stand-in");
        await fixture.WriteAsync("images/diagram.svg", "<svg/>");
        await fixture.WriteAsync("javascripts/extra.js", "console.log('hi');");
        await fixture.WriteAsync("guide/intro.md", "# Intro\nbody");

        var copied = DocsAssetCopier.Copy((DirectoryPath)fixture.Input, (DirectoryPath)fixture.Output, PathFilter.Empty);

        await Assert.That(File.Exists(Path.Combine(fixture.Output, "images", "logo.png"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "images", "diagram.svg"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "javascripts", "extra.js"))).IsTrue();
        await Assert.That(copied).IsEqualTo(3);
    }

    /// <summary>Markdown files are skipped — pages are emitted by the render pipeline, not by the asset copier.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MarkdownFilesAreSkipped()
    {
        using var fixture = ScratchTree.Create();
        await fixture.WriteAsync("guide/intro.md", "# Intro");
        await fixture.WriteAsync("README.md", "# Readme");

        DocsAssetCopier.Copy((DirectoryPath)fixture.Input, (DirectoryPath)fixture.Output, PathFilter.Empty);

        await Assert.That(File.Exists(Path.Combine(fixture.Output, "guide", "intro.md"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "README.md"))).IsFalse();
    }

    /// <summary>The Nav plugin's <c>.pages</c> override files are skipped — they're build-time configuration, not site content.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PagesFilesAreSkipped()
    {
        using var fixture = ScratchTree.Create();
        await fixture.WriteAsync("guide/.pages", "title: Guide");

        DocsAssetCopier.Copy((DirectoryPath)fixture.Input, (DirectoryPath)fixture.Output, PathFilter.Empty);

        await Assert.That(File.Exists(Path.Combine(fixture.Output, "guide", ".pages"))).IsFalse();
    }

    /// <summary>Dotfiles and dot-prefixed directories (<c>.git</c>, <c>.cache</c>, etc.) are skipped to avoid leaking source-control / build state.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task HiddenPathsAreSkipped()
    {
        using var fixture = ScratchTree.Create();
        await fixture.WriteAsync(".git/config", "git stuff");
        await fixture.WriteAsync(".cache/data.bin", "cache stuff");
        await fixture.WriteAsync("images/.DS_Store", "mac state");

        DocsAssetCopier.Copy((DirectoryPath)fixture.Input, (DirectoryPath)fixture.Output, PathFilter.Empty);

        await Assert.That(File.Exists(Path.Combine(fixture.Output, ".git", "config"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(fixture.Output, ".cache", "data.bin"))).IsFalse();
        await Assert.That(File.Exists(Path.Combine(fixture.Output, "images", ".DS_Store"))).IsFalse();
    }

    /// <summary>An empty input root returns zero copies and doesn't fail.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MissingInputRootReturnsZero()
    {
        var phantom = Path.Combine(Path.GetTempPath(), "smkd-asset-missing-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "smkd-asset-out-" + Guid.NewGuid().ToString("N"));
        try
        {
            var copied = DocsAssetCopier.Copy((DirectoryPath)phantom, (DirectoryPath)output, PathFilter.Empty);
            await Assert.That(copied).IsEqualTo(0);
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    /// <summary>Disposable input/output directory pair scoped to one test.</summary>
    private sealed class ScratchTree : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchTree"/> class.</summary>
        /// <param name="root">Absolute fixture root.</param>
        private ScratchTree(string root)
        {
            Root = root;
            Input = Path.Combine(root, "docs");
            Output = Path.Combine(root, "site");
            Directory.CreateDirectory(Input);
            Directory.CreateDirectory(Output);
        }

        /// <summary>Gets the fixture root directory.</summary>
        public string Root { get; }

        /// <summary>Gets the docs input directory.</summary>
        public string Input { get; }

        /// <summary>Gets the site output directory.</summary>
        public string Output { get; }

        /// <summary>Creates a fresh fixture under the system temp directory.</summary>
        /// <returns>A new fixture; caller must dispose.</returns>
        public static ScratchTree Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "smkd-assetcopy-" + Guid.NewGuid().ToString("N"));
            return new(root);
        }

        /// <summary>Writes <paramref name="content"/> to <paramref name="relativePath"/> under <see cref="Input"/>.</summary>
        /// <param name="relativePath">Path relative to the docs input root.</param>
        /// <param name="content">UTF-8 file contents.</param>
        /// <returns>Async I/O task.</returns>
        public Task WriteAsync(string relativePath, string content)
        {
            var path = Path.Combine(Input, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            return File.WriteAllTextAsync(path, content);
        }

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
                // Best-effort cleanup.
            }
        }
    }
}
