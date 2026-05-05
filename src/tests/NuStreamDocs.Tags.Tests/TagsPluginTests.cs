// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Tags.Tests;

/// <summary>End-to-end + lifecycle tests for <c>TagsPlugin</c> and the index writer.</summary>
public class TagsPluginTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new TagsPlugin().Name.SequenceEqual("tags"u8)).IsTrue();

    /// <summary>Default options write to <c>tags/index.html</c>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultOptionsRoundtrip()
    {
        await Assert.That(TagsOptions.Default.OutputSubdirectory).IsEqualTo("tags");
        await Assert.That(TagsOptions.Default.IndexFileName).IsEqualTo("index.html");
    }

    /// <summary>Pages with no tags are silently skipped.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PageWithoutTagsSkipped()
    {
        using var temp = new TagsTempDir();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "notag.md"), "no frontmatter");

        var plugin = new TagsPlugin();
        await plugin.DiscoverAsync(new BuildDiscoverContext(temp.Root, "/out", []), CancellationToken.None);

        await Assert.That(Directory.Exists(Path.Combine(temp.Root, "tags"))).IsFalse();
    }

    /// <summary>Tagged pages produce an index plus per-tag listings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EndToEndEmitsIndexAndPerTagPages()
    {
        using var temp = new TagsTempDir();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "first.md"), "---\ntags:\n  - alpha\n  - beta\n---\n# First\n\nbody");
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "second.md"), "---\ntags:\n  - alpha\n---\n# Second\n\nbody");

        var plugin = new TagsPlugin();
        await plugin.DiscoverAsync(new BuildDiscoverContext(temp.Root, "/out", []), CancellationToken.None);

        var indexMd = await File.ReadAllTextAsync(Path.Combine(temp.Root, "tags", "index.md"));
        await Assert.That(indexMd).Contains("alpha");
        await Assert.That(indexMd).Contains("beta");

        var alphaMd = await File.ReadAllTextAsync(Path.Combine(temp.Root, "tags", "alpha.md"));
        await Assert.That(alphaMd).Contains("First");
        await Assert.That(alphaMd).Contains("Second");
    }

    /// <summary>Custom options write to a different subdirectory and index filename.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomOptionsHonored()
    {
        using var temp = new TagsTempDir();
        await File.WriteAllTextAsync(Path.Combine(temp.Root, "p.md"), "---\ntags:\n  - foo\n---\n# Page\n\nbody");

        var options = new TagsOptions("topics", "all.html");
        var plugin = new TagsPlugin(options);
        await plugin.DiscoverAsync(new BuildDiscoverContext(temp.Root, "/out", []), CancellationToken.None);

        // Discover writes markdown source files; the html filename option drives the slug naming convention.
        await Assert.That(Directory.Exists(Path.Combine(temp.Root, "topics"))).IsTrue();
    }

    /// <summary>RelativePathToUrlPath swaps <c>.md</c> for <c>.html</c> and normalizes separators.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathSwapsExtension() =>
        await Assert.That(Encoding.UTF8.GetString(TagsIndexWriter.RelativePathToUrlPath("guide\\intro.md"))).IsEqualTo("guide/intro.html");

    /// <summary>RelativePathToUrlPath returns empty for empty input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathEmpty() =>
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath(string.Empty).Length).IsEqualTo(0);

    /// <summary>RelativePathToUrlPath leaves non-md inputs alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathNonMd() =>
        await Assert.That(Encoding.UTF8.GetString(TagsIndexWriter.RelativePathToUrlPath("assets/logo.png"))).IsEqualTo("assets/logo.png");

    /// <summary>UseTags() registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseTagsRegisters() => await Assert.That(new DocBuilder().UseTags()).IsTypeOf<DocBuilder>();

    /// <summary>UseTags(options) registers the plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseTagsWithOptionsRegisters() =>
        await Assert.That(new DocBuilder().UseTags(TagsOptions.Default)).IsTypeOf<DocBuilder>();

    /// <summary>UseTags rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseTagsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderTagsExtensions.UseTags(null!));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>UseTags(options) rejects null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task UseTagsOptionsRejectsNullBuilder()
    {
        var ex = Assert.Throws<ArgumentNullException>(static () => DocBuilderTagsExtensions.UseTags(null!, TagsOptions.Default));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Disposable scratch directory.</summary>
    private sealed class TagsTempDir : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TagsTempDir"/> class.</summary>
        public TagsTempDir()
        {
            Root = Path.Combine(Path.GetTempPath(), "smkd-tags-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        /// <summary>Gets the absolute path to the scratch directory.</summary>
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
