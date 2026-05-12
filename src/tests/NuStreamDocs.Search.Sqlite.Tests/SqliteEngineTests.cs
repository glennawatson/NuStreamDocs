// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SQLitePCL;

namespace NuStreamDocs.Search.Sqlite.Tests;

/// <summary>Coverage for <see cref="SqliteEngine"/> — exclude-prefix filtering and manifest metadata.</summary>
public class SqliteEngineTests
{
    /// <summary>FormatName and ManifestFileName are the expected literals.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MetadataLiterals()
    {
        var e = new SqliteEngine([], indexFullBody: true);
        await Assert.That(System.Text.Encoding.UTF8.GetString(e.FormatName)).IsEqualTo("sqlite");
        await Assert.That(System.Text.Encoding.UTF8.GetString(e.ManifestFileName)).IsEqualTo("/search.db");
    }

    /// <summary>Excluded-prefix pages are dropped from the written database.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExcludePrefixDropsPages()
    {
        using TempDir dir = new();
        var e = new SqliteEngine([[.. "api/"u8]], indexFullBody: true);
        var manifest = e.Write(dir.Root, [Doc("/guide/intro.html"), Doc("/api/Foo.html"), Doc("/api/Bar.html")]);
        await Assert.That(File.Exists(manifest.Value)).IsTrue();

        Batteries_V2.Init();
        raw.sqlite3_open_v2(manifest.Value, out var db, raw.SQLITE_OPEN_READONLY, null);
        try
        {
            raw.sqlite3_prepare_v2(db, "SELECT count(*) FROM pages", out var stmt);
            raw.sqlite3_step(stmt);
            await Assert.That(raw.sqlite3_column_int(stmt, 0)).IsEqualTo(1);
            raw.sqlite3_finalize(stmt);
        }
        finally
        {
            raw.sqlite3_close_v2(db);
        }
    }

    /// <summary>Builds a <see cref="SearchDocument"/> with a fixed title/body and the given URL.</summary>
    /// <param name="url">Root-relative URL.</param>
    /// <returns>The document.</returns>
    private static SearchDocument Doc(string url) =>
        new(System.Text.Encoding.UTF8.GetBytes(url), System.Text.Encoding.UTF8.GetBytes("T"), System.Text.Encoding.UTF8.GetBytes("indexed body text"));
}
