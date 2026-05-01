// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using NuStreamDocs.Building;

namespace NuStreamDocs.Tags.Tests;

/// <summary>End-to-end + lifecycle tests for <c>TagsPlugin</c> and the index writer.</summary>
public class TagsPluginTests
{
    /// <summary>Plugin name is stable.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameIsStable() => await Assert.That(new TagsPlugin().Name).IsEqualTo("tags");

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
        var plugin = new TagsPlugin();
        await plugin.OnConfigureAsync(new(default, "/in", temp.Root, []), CancellationToken.None);

        var sink = new ArrayBufferWriter<byte>(8);
        sink.Write("<p>no tags</p>"u8);
        await plugin.OnRenderPageAsync(new("notag.md", "no frontmatter"u8.ToArray(), sink), CancellationToken.None);
        await plugin.OnFinaliseAsync(new(temp.Root), CancellationToken.None);

        await Assert.That(Directory.Exists(Path.Combine(temp.Root, "tags"))).IsFalse();
    }

    /// <summary>Tagged pages produce an index plus per-tag listings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EndToEndEmitsIndexAndPerTagPages()
    {
        using var temp = new TagsTempDir();
        var plugin = new TagsPlugin();
        await plugin.OnConfigureAsync(new(default, "/in", temp.Root, []), CancellationToken.None);

        const string Source1 = "---\ntags:\n  - alpha\n  - beta\n---\nbody";
        var sink1 = new ArrayBufferWriter<byte>(64);
        sink1.Write("<h1>First</h1>"u8);
        await plugin.OnRenderPageAsync(new("first.md", Encoding.UTF8.GetBytes(Source1), sink1), CancellationToken.None);

        const string Source2 = "---\ntags:\n  - alpha\n---\nbody";
        var sink2 = new ArrayBufferWriter<byte>(64);
        sink2.Write("<h1>Second</h1>"u8);
        await plugin.OnRenderPageAsync(new("second.md", Encoding.UTF8.GetBytes(Source2), sink2), CancellationToken.None);

        await plugin.OnFinaliseAsync(new(temp.Root), CancellationToken.None);

        var indexHtml = await File.ReadAllTextAsync(Path.Combine(temp.Root, "tags", "index.html"));
        await Assert.That(indexHtml).Contains("alpha");
        await Assert.That(indexHtml).Contains("beta");

        var alphaHtml = await File.ReadAllTextAsync(Path.Combine(temp.Root, "tags", "alpha.html"));
        await Assert.That(alphaHtml).Contains("First");
        await Assert.That(alphaHtml).Contains("Second");
    }

    /// <summary>The H1 fallback path is used when the rendered HTML has no heading.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FallbackTitleWhenNoH1()
    {
        using var temp = new TagsTempDir();
        var plugin = new TagsPlugin();
        await plugin.OnConfigureAsync(new(default, "/in", temp.Root, []), CancellationToken.None);

        const string Source = "---\ntags:\n  - solo\n---\nbody";
        var sink = new ArrayBufferWriter<byte>(64);
        sink.Write("<p>just body, no h1</p>"u8);
        await plugin.OnRenderPageAsync(new("only.md", Encoding.UTF8.GetBytes(Source), sink), CancellationToken.None);
        await plugin.OnFinaliseAsync(new(temp.Root), CancellationToken.None);

        // Fallback is the URL.
        var soloHtml = await File.ReadAllTextAsync(Path.Combine(temp.Root, "tags", "solo.html"));
        await Assert.That(soloHtml).Contains("only.html");
    }

    /// <summary>Custom options write to a different subdirectory and index filename.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CustomOptionsHonoured()
    {
        using var temp = new TagsTempDir();
        var options = new TagsOptions("topics", "all.html");
        var plugin = new TagsPlugin(options);
        await plugin.OnConfigureAsync(new(default, "/in", temp.Root, []), CancellationToken.None);

        const string Source = "---\ntags:\n  - foo\n---\nbody";
        var sink = new ArrayBufferWriter<byte>(64);
        sink.Write("<h1>Page</h1>"u8);
        await plugin.OnRenderPageAsync(new("p.md", Encoding.UTF8.GetBytes(Source), sink), CancellationToken.None);
        await plugin.OnFinaliseAsync(new(temp.Root), CancellationToken.None);

        await Assert.That(File.Exists(Path.Combine(temp.Root, "topics", "all.html"))).IsTrue();
        await Assert.That(File.Exists(Path.Combine(temp.Root, "topics", "foo.html"))).IsTrue();
    }

    /// <summary>RelativePathToUrlPath swaps <c>.md</c> for <c>.html</c> and normalises separators.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathSwapsExtension() =>
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath("guide\\intro.md")).IsEqualTo("guide/intro.html");

    /// <summary>RelativePathToUrlPath returns empty for empty input.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathEmpty() =>
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath(string.Empty)).IsEqualTo(string.Empty);

    /// <summary>RelativePathToUrlPath leaves non-md inputs alone.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RelativePathToUrlPathNonMd() =>
        await Assert.That(TagsIndexWriter.RelativePathToUrlPath("assets/logo.png")).IsEqualTo("assets/logo.png");

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
