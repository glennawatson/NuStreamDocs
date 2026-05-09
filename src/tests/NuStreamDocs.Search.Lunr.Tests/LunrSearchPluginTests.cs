// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Search.Lunr.Tests;

/// <summary>End-to-end + coverage tests for <see cref="LunrSearchPlugin"/>.</summary>
public class LunrSearchPluginTests
{
    /// <summary>Default mode writes a single search_index.json with config + docs.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultModeWritesSingleIndex()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page\n\nbody words");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseLunrSearch()
            .BuildAsync();

        var indexPath = Path.Combine(fixture.Site, "search", "search_index.json");
        await Assert.That(File.Exists(indexPath)).IsTrue();

        var indexBytes = await File.ReadAllBytesAsync(indexPath);
        using var doc = JsonDocument.Parse(indexBytes);
        await Assert.That(doc.RootElement.GetProperty("config"u8).GetProperty("lang"u8).GetString()).IsEqualTo("en");

        var docs = doc.RootElement.GetProperty("docs"u8);
        await Assert.That(docs.GetArrayLength()).IsEqualTo(1);
        await Assert.That(docs[0].GetProperty("title"u8).GetString()).IsEqualTo("Page");
    }

    /// <summary>Empty Scan input is skipped without adding a document.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyHtmlSkipsDocument()
    {
        LunrSearchPlugin plugin = new();
        ScanPage(plugin, "page.md", default, default);
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }

    /// <summary>HTML without a heading falls back to the filename stem as the title.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoHeadingFallsBackToFilenameStem()
    {
        LunrSearchPlugin plugin = new();
        ScanPage(plugin, "guide/intro.md", default, "<p>just text</p>"u8);
        var docs = plugin.DocumentsSnapshot();
        await Assert.That(docs.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(docs[0].Title)).IsEqualTo("intro");
    }

    /// <summary>SearchableFrontmatterKeys appends extracted bytes into the document text.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SearchableFrontmatterKeysAppendBytes()
    {
        LunrSearchPlugin plugin = new(LunrOptions.Default with { SearchableFrontmatterKeys = [[.. "tags"u8]] });
        ScanPage(plugin, "page.md", "---\ntags: [foo, bar]\n---\nbody"u8, "<h1>Hi</h1><p>body</p>"u8);
        var docs = plugin.DocumentsSnapshot();
        await Assert.That(docs.Length).IsEqualTo(1);
        await Assert.That(Encoding.UTF8.GetString(docs[0].Text)).Contains("foo");
    }

    /// <summary>FinalizeAsync with an empty output root is a silent no-op.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task FinalizeAsyncEmptyRootNoOp()
    {
        LunrSearchPlugin plugin = new();
        await plugin.FinalizeAsync(new(string.Empty, []), CancellationToken.None);
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }

    /// <summary>Compression.Smallest produces both .gz and .br siblings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SmallestCompressionWritesGzipAndBrotli()
    {
        using var fixture = TempBuildFixture.Create();
        LunrSearchPlugin plugin = new(LunrOptions.Default with { Compression = SearchCompression.Smallest });

        await plugin.ConfigureAsync(new(fixture.Root, fixture.Root, [], new()), CancellationToken.None);
        ScanPage(plugin, "a.md", default, "<h1>Hi</h1><p>body content</p>"u8);
        await plugin.FinalizeAsync(new(fixture.Root, []), CancellationToken.None);

        var json = Path.Combine(fixture.Root, "search", "search_index.json");
        await Assert.That(File.Exists(json + ".gz")).IsTrue();
        await Assert.That(File.Exists(json + ".br")).IsTrue();
    }

    /// <summary>Compression.Default writes only the .gz sibling.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultCompressionWritesOnlyGzip()
    {
        using var fixture = TempBuildFixture.Create();
        LunrSearchPlugin plugin = new(LunrOptions.Default with { Compression = SearchCompression.Default });

        await plugin.ConfigureAsync(new(fixture.Root, fixture.Root, [], new()), CancellationToken.None);
        ScanPage(plugin, "a.md", default, "<h1>Hi</h1><p>body</p>"u8);
        await plugin.FinalizeAsync(new(fixture.Root, []), CancellationToken.None);

        var json = Path.Combine(fixture.Root, "search", "search_index.json");
        await Assert.That(File.Exists(json + ".gz")).IsTrue();
        await Assert.That(File.Exists(json + ".br")).IsFalse();
    }

    /// <summary>WriteHeadExtra emits the Lunr index path as the search-index discovery target.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraEmitsLunrIndexPath()
    {
        LunrSearchPlugin plugin = new();
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains("search_index.json");
    }

    /// <summary>WriteHeadExtra rejects a null writer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraNullWriterThrows() =>
        await Assert.That(() => new LunrSearchPlugin().WriteHeadExtra(null!)).Throws<ArgumentNullException>();

    /// <summary>Name returns "search".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        LunrSearchPlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("search"u8)).IsTrue();
    }

    /// <summary>Drives one Scan call against the plugin.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="relativePath">Source-relative markdown path.</param>
    /// <param name="source">Markdown bytes (frontmatter + body).</param>
    /// <param name="html">Rendered HTML bytes.</param>
    private static void ScanPage(LunrSearchPlugin plugin, string relativePath, ReadOnlySpan<byte> source, ReadOnlySpan<byte> html)
    {
        PageScanContext ctx = new(relativePath, source, html);
        plugin.Scan(in ctx);
    }

    /// <summary>Disposable fixture: writable temp docs/site directories that auto-clean.</summary>
    private sealed class TempBuildFixture : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TempBuildFixture"/> class.</summary>
        /// <param name="root">Fixture root directory.</param>
        private TempBuildFixture(string root)
        {
            Root = root;
            Docs = Path.Combine(root, "docs");
            Site = Path.Combine(root, "site");
            Directory.CreateDirectory(Docs);
        }

        /// <summary>Gets the fixture root directory.</summary>
        public string Root { get; }

        /// <summary>Gets the absolute path to the input docs directory.</summary>
        public string Docs { get; }

        /// <summary>Gets the absolute path to the output site directory.</summary>
        public string Site { get; }

        /// <summary>Creates a fresh fixture under <c>Path.GetTempPath</c>.</summary>
        /// <returns>A new fixture; caller must dispose.</returns>
        public static TempBuildFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "smkd-lunr-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(root);
            return new(root);
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
