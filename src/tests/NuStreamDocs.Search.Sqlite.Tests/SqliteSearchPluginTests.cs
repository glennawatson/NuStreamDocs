// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Text;
using NuStreamDocs.Building;
using NuStreamDocs.Plugins;
using SQLitePCL;

namespace NuStreamDocs.Search.Sqlite.Tests;

/// <summary>End-to-end + coverage tests for <see cref="SqliteSearchPlugin"/>.</summary>
public class SqliteSearchPluginTests
{
    /// <summary>Default mode writes a single search.db under the search subdirectory.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DefaultModeWritesSingleDatabase()
    {
        using var fixture = TempBuildFixture.Create();
        await File.WriteAllTextAsync(Path.Combine(fixture.Docs, "page.md"), "# Page\n\nbody words about widgets");

        await new DocBuilder()
            .WithInput(fixture.Docs)
            .WithOutput(fixture.Site)
            .UseSqliteSearch()
            .BuildAsync();

        var dbPath = Path.Combine(fixture.Site, "search", "search.db");
        await Assert.That(File.Exists(dbPath)).IsTrue();
        await Assert.That(PageCount(dbPath)).IsGreaterThanOrEqualTo(1);
    }

    /// <summary>Empty Scan input is skipped without adding a document.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyHtmlSkipsDocument()
    {
        SqliteSearchPlugin plugin = new();
        ScanPage(plugin, "page.md", default, default);
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }

    /// <summary>HTML without a heading falls back to the filename stem as the title.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NoHeadingFallsBackToFilenameStem()
    {
        SqliteSearchPlugin plugin = new();
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
        SqliteSearchPlugin plugin = new(SqliteOptions.Default with { SearchableFrontmatterKeys = [[.. "tags"u8]] });
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
        SqliteSearchPlugin plugin = new();
        await plugin.FinalizeAsync(new(string.Empty, []), CancellationToken.None);
        await Assert.That(plugin.DocumentsSnapshot().Length).IsEqualTo(0);
    }

    /// <summary>WriteHeadExtra emits the search-index meta and the bind-script tag.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task WriteHeadExtraEmitsMetaAndBindScript()
    {
        SqliteSearchPlugin plugin = new();
        ArrayBufferWriter<byte> sink = new();
        plugin.WriteHeadExtra(sink);
        var rendered = Encoding.UTF8.GetString(sink.WrittenSpan);
        await Assert.That(rendered).Contains("nustreamdocs:search-index");
        await Assert.That(rendered).Contains("/assets/javascripts/sqlite-bind.js");
        await Assert.That(rendered).Contains("/assets/javascripts/sql.js-httpvfs.js");
    }

    /// <summary>StaticAssets ships the loader, worker, wasm, and bind script with non-empty bytes.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task StaticAssetsShipRuntimeAndGlue()
    {
        var assets = new SqliteSearchPlugin().StaticAssets;
        var paths = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < assets.Length; i++)
        {
            paths.Add(assets[i].Path.Value);
            await Assert.That(assets[i].Bytes.Length).IsGreaterThan(0);
        }

        await Assert.That(paths.Contains("assets/javascripts/sql.js-httpvfs.js")).IsTrue();
        await Assert.That(paths.Contains("assets/javascripts/sqlite.worker.js")).IsTrue();
        await Assert.That(paths.Contains("assets/javascripts/sql-wasm.wasm")).IsTrue();
        await Assert.That(paths.Contains("assets/javascripts/sqlite-bind.js")).IsTrue();
    }

    /// <summary>Excluded-prefix pages are dropped from the written database.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExcludePrefixDropsPages()
    {
        using var fixture = TempBuildFixture.Create();
        SqliteSearchPlugin plugin = new(SqliteOptions.Default.WithExcludePathPrefixes("api/"));

        await plugin.ConfigureAsync(new(fixture.Root, fixture.Root, [], new()), CancellationToken.None);
        ScanPage(plugin, "guide/intro.md", default, "<h1>Guide</h1><p>indexed body text</p>"u8);
        ScanPage(plugin, "api/Foo.md", default, "<h1>Foo</h1><p>indexed body text</p>"u8);
        await plugin.FinalizeAsync(new(fixture.Root, []), CancellationToken.None);

        var dbPath = Path.Combine(fixture.Root, "search", "search.db");
        await Assert.That(File.Exists(dbPath)).IsTrue();
        await Assert.That(PageCount(dbPath)).IsEqualTo(1);
    }

    /// <summary>Name returns "search".</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task NameAccessor()
    {
        SqliteSearchPlugin plugin = new();
        await Assert.That(plugin.Name.SequenceEqual("search"u8)).IsTrue();
    }

    /// <summary>The pinned runtime version is exposed.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PinnedRuntimeVersionExposed()
    {
        await Assert.That(SqliteSearchPlugin.PinnedRuntimeVersion).IsNotEmpty();
    }

    /// <summary>Drives one Scan call against the plugin.</summary>
    /// <param name="plugin">Plugin under test.</param>
    /// <param name="relativePath">Source-relative markdown path.</param>
    /// <param name="source">Markdown bytes (frontmatter + body).</param>
    /// <param name="html">Rendered HTML bytes.</param>
    private static void ScanPage(SqliteSearchPlugin plugin, string relativePath, ReadOnlySpan<byte> source, ReadOnlySpan<byte> html)
    {
        PageScanContext ctx = new(relativePath, source, html);
        plugin.Scan(in ctx);
    }

    /// <summary>Opens the database read-only and returns the row count of the <c>pages</c> table.</summary>
    /// <param name="dbPath">Absolute path to the database.</param>
    /// <returns>Row count.</returns>
    private static int PageCount(string dbPath)
    {
        Batteries_V2.Init();
        raw.sqlite3_open_v2(dbPath, out var db, raw.SQLITE_OPEN_READONLY, null);
        try
        {
            raw.sqlite3_prepare_v2(db, "SELECT count(*) FROM pages", out var stmt);
            try
            {
                raw.sqlite3_step(stmt);
                return raw.sqlite3_column_int(stmt, 0);
            }
            finally
            {
                raw.sqlite3_finalize(stmt);
            }
        }
        finally
        {
            raw.sqlite3_close_v2(db);
        }
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
            var root = Path.Combine(Path.GetTempPath(), "smkd-sqlite-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
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
