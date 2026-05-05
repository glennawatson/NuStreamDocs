// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;

namespace NuStreamDocs.Search.Tests;

/// <summary>End-to-end tests for the <c>SearchPlugin</c>.</summary>
public class SearchPluginTests
{
    /// <summary>Default mode should write a Pagefind manifest + record per page.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PagefindModeWritesManifestAndRecords()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "intro.md"), "# Intro\n\nhello world");
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "guide.md"), "# Guide\n\nmore content");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseSearch()
            .BuildAsync();

        var manifestPath = Path.Combine(fixture.Site, "search", "pagefind-entry.json");
        await Assert.That(File.Exists(manifestPath)).IsTrue();

        var manifestBytes = await File.ReadAllBytesAsync(manifestPath);
        using var doc = JsonDocument.Parse(manifestBytes);
        var records = doc.RootElement.GetProperty("records"u8);
        await Assert.That(records.GetArrayLength()).IsEqualTo(2);

        var recordsDir = Path.Combine(fixture.Site, "search", "pagefind-records");
        await Assert.That(Directory.GetFiles(recordsDir, "*.json").Length).IsEqualTo(2);
    }

    /// <summary>Lunr mode should write a single search_index.json with config + docs.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task LunrModeWritesSingleIndex()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page\n\nbody words");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseSearch(static opts => opts with { Format = SearchFormat.Lunr })
            .BuildAsync();

        var indexPath = Path.Combine(fixture.Site, "search", "search_index.json");
        await Assert.That(File.Exists(indexPath)).IsTrue();

        var indexBytes = await File.ReadAllBytesAsync(indexPath);
        using var doc = JsonDocument.Parse(indexBytes);
        await Assert.That(doc.RootElement.GetProperty("config"u8).GetProperty("lang"u8).GetString()).IsEqualTo("en");

        var docs = doc.RootElement.GetProperty("docs"u8);
        await Assert.That(docs.GetArrayLength()).IsEqualTo(1);
        await Assert.That(docs[0].GetProperty("title"u8).GetString()).IsEqualTo("Page");
        await Assert.That(docs[0].GetProperty("location"u8).GetString()).IsEqualTo("/page.html");
    }

    /// <summary>An empty Scan input is skipped without adding a document.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyHtmlSkipsDocument()
    {
        var plugin = new SearchPlugin();
        ScanPage(plugin, "page.md", default, default);
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }

    /// <summary>HTML without a heading falls back to the filename stem as the title.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoHeadingFallsBackToFilenameStem()
    {
        var plugin = new SearchPlugin();
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
        var plugin = new SearchPlugin(SearchOptions.Default with { SearchableFrontmatterKeys = [[.. "tags"u8]] });
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
        var plugin = new SearchPlugin();
        await plugin.FinalizeAsync(new BuildFinalizeContext(string.Empty, []), CancellationToken.None);
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }

    /// <summary>Compression.Smallest produces both .gz and .br siblings.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SmallestCompressionWritesGzipAndBrotli()
    {
        using var fixture = TempBuildFixture.Create();
        var plugin = new SearchPlugin(SearchOptions.Default with { Format = SearchFormat.Lunr, Compression = SearchCompression.Smallest });

        await plugin.ConfigureAsync(new BuildConfigureContext(fixture.Root, fixture.Root, [], new()), CancellationToken.None);
        ScanPage(plugin, "a.md", default, "<h1>Hi</h1><p>body content</p>"u8);
        await plugin.FinalizeAsync(new BuildFinalizeContext(fixture.Root, []), CancellationToken.None);

        var json = Path.Combine(fixture.Root, "search", "search_index.json");
        await Assert.That(File.Exists(json + ".gz")).IsTrue();
        await Assert.That(File.Exists(json + ".br")).IsTrue();
    }

    /// <summary>Compression.Default writes only the .gz sibling (no Brotli).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultCompressionWritesOnlyGzip()
    {
        using var fixture = TempBuildFixture.Create();
        var plugin = new SearchPlugin(SearchOptions.Default with { Format = SearchFormat.Lunr, Compression = SearchCompression.Default });

        await plugin.ConfigureAsync(new BuildConfigureContext(fixture.Root, fixture.Root, [], new()), CancellationToken.None);
        ScanPage(plugin, "a.md", default, "<h1>Hi</h1><p>body</p>"u8);
        await plugin.FinalizeAsync(new BuildFinalizeContext(fixture.Root, []), CancellationToken.None);

        var json = Path.Combine(fixture.Root, "search", "search_index.json");
        await Assert.That(File.Exists(json + ".gz")).IsTrue();
        await Assert.That(File.Exists(json + ".br")).IsFalse();
    }

    /// <summary>WriteHeadExtra emits Pagefind manifest path by default and the Lunr index path when configured.</summary>
    /// <param name="format">Format selection.</param>
    /// <param name="expectedFragment">Substring expected in the head bytes.</param>
    /// <returns>Async test.</returns>
    [Test]
    [Arguments(SearchFormat.Pagefind, "pagefind-entry.json")]
    [Arguments(SearchFormat.Lunr, "search_index.json")]
    public async Task WriteHeadExtraFormatRouting(SearchFormat format, string expectedFragment)
    {
        var plugin = new SearchPlugin(SearchOptions.Default with { Format = format });
        var sink = new ArrayBufferWriter<byte>();
        plugin.WriteHeadExtra(sink);
        await Assert.That(Encoding.UTF8.GetString(sink.WrittenSpan)).Contains(expectedFragment);
    }

    /// <summary>WriteHeadExtra rejects a null writer.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraNullWriterThrows() =>
        await Assert.That(() => new SearchPlugin().WriteHeadExtra(null!)).Throws<ArgumentNullException>();

    /// <summary>Drives one Scan call against the plugin.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="relativePath">Source-relative markdown path.</param>
    /// <param name="source">Markdown bytes (frontmatter + body).</param>
    /// <param name="html">Rendered HTML bytes.</param>
    private static void ScanPage(SearchPlugin plugin, string relativePath, ReadOnlySpan<byte> source, ReadOnlySpan<byte> html)
    {
        var ctx = new PageScanContext(relativePath, source, html);
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
            var root = Path.Combine(Path.GetTempPath(), "smkd-search-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
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
