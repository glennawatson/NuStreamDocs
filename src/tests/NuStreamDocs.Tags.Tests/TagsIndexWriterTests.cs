// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Tags.Tests;

/// <summary>Branch-coverage tests for TagsIndexWriter.</summary>
public class TagsIndexWriterTests
{
    /// <summary>Empty input yields no files.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyEntriesNoFiles()
    {
        using var temp = new ScratchDir();
        TagsIndexWriter.Write(temp.Root, TagsOptions.Default, []);
        await Assert.That(Directory.Exists(Path.Combine(temp.Root, "tags"))).IsFalse();
    }

    /// <summary>RelativePathToUrlPath handles md, non-md, empty, and backslashes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathBranches()
    {
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath("guide/intro.md")).IsEqualTo("guide/intro.html");
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath("guide/intro.MD")).IsEqualTo("guide/intro.html");
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath("guide/intro.txt")).IsEqualTo("guide/intro.txt");
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath("a\\b.md")).IsEqualTo("a/b.html");
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath(string.Empty)).IsEqualTo(string.Empty);
    }

    /// <summary>Write produces an index page and per-tag pages with HTML-escaped content.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteEmitsExpectedFiles()
    {
        using var temp = new ScratchDir();
        TagEntry[] entries =
        [
            new("Alpha & Beta", "guide/intro.html", "<Title>"),
            new("Alpha & Beta", "ref/api.html", "API"),
            new("gamma", "guide/intro.html", "<Title>"),
        ];
        TagsIndexWriter.Write(temp.Root, TagsOptions.Default, entries);
        var tagsDir = Path.Combine(temp.Root, "tags");
        await Assert.That(File.Exists(Path.Combine(tagsDir, "index.html"))).IsTrue();
        var alphaPath = Path.Combine(tagsDir, "alpha-beta.html");
        await Assert.That(File.Exists(alphaPath)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(tagsDir, "gamma.html"))).IsTrue();

        var index = await File.ReadAllTextAsync(Path.Combine(tagsDir, "index.html"));
        await Assert.That(index).Contains("Alpha &amp; Beta");
        await Assert.That(index).Contains("(2)");

        var alpha = await File.ReadAllTextAsync(alphaPath);
        await Assert.That(alpha).Contains("&lt;Title&gt;");
    }

    /// <summary>A tag whose entire body slugifies to nothing falls back to "tag".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SymbolOnlyTagSlug()
    {
        using var temp = new ScratchDir();
        TagsIndexWriter.Write(temp.Root, TagsOptions.Default, [new("***", "p.html", "P")]);
        await Assert.That(File.Exists(Path.Combine(temp.Root, "tags", "tag.html"))).IsTrue();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class ScratchDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ScratchDir"/> class.</summary>
        public ScratchDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-tags-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path of the scratch directory.</summary>
        public string Root { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
        }
    }
}
