// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using SQLitePCL;

namespace NuStreamDocs.Search.Sqlite.Tests;

/// <summary>Coverage for <see cref="SqliteIndexWriter"/> — builds an FTS5 search.db and reads it back.</summary>
public class SqliteIndexWriterTests
{
    /// <summary>Names the database written by index tests.</summary>
    private const string DatabaseFileName = "search.db";

    /// <summary>Identifies the sample document searched by body and prefix tests.</summary>
    private const string DocumentUrl = "/a.html";

    /// <summary>Places the trailing search term beyond the stored excerpt.</summary>
    private const int ExcerptOverflowLength = 50;

    /// <summary>MATCH returns the page whose body contains the term.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MatchFindsBodyTerm()
    {
        using TempDir dir = new();
        var db = Path.Combine(dir.Root, DatabaseFileName);
        SqliteIndexWriter.Write(
            db,
            [Doc(DocumentUrl, "Alpha", "the quick brown fox"), Doc("/b.html", "Beta", "lazy dog sleeps")],
            true);
        var hits = QueryUrlsRanked(db, "quick");
        await Assert.That(hits).Contains(DocumentUrl);
        await Assert.That(hits).DoesNotContain("/b.html");
    }

    /// <summary>A title hit ranks above a body-only hit for the same term.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task TitleHitOutranksBodyHit()
    {
        using TempDir dir = new();
        var db = Path.Combine(dir.Root, DatabaseFileName);
        SqliteIndexWriter.Write(
            db,
            [
                Doc("/title.html", "widget guide", "nothing relevant here"),
                Doc("/body.html", "Other", "a page about the widget internals")
            ],
            true);
        var ranked = QueryUrlsRanked(db, "widget");
        await Assert.That(ranked[0]).IsEqualTo("/title.html");
    }

    /// <summary>A short prefix query (3 chars) hits a longer token.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task PrefixQueryHitsLongerToken()
    {
        using TempDir dir = new();
        var db = Path.Combine(dir.Root, DatabaseFileName);
        SqliteIndexWriter.Write(db, [Doc(DocumentUrl, "A", "configuration matters")], true);
        await Assert.That(QueryUrlsRanked(db, "con*")).Contains(DocumentUrl);
    }

    /// <summary>IndexFullBody=false stores only a short body excerpt.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ExcerptModeTruncatesBody()
    {
        using TempDir dir = new();
        var db = Path.Combine(dir.Root, DatabaseFileName);
        var longBody = $"{new string('x', SqliteOptions.ExcerptByteLimit + ExcerptOverflowLength)} uniquetokenatend";
        SqliteIndexWriter.Write(db, [Doc(DocumentUrl, "A", longBody)], false);

        // The trailing token sits past ExcerptByteLimit, so it must not be searchable.
        await Assert.That(QueryUrlsRanked(db, "uniquetokenatend")).IsEmpty();
        await Assert.That(StoredBodyLength(db, DocumentUrl)).IsLessThanOrEqualTo(SqliteOptions.ExcerptByteLimit);
    }

    /// <summary>Rebuilding over an existing file replaces it cleanly.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RebuildReplacesFile()
    {
        using TempDir dir = new();
        var db = Path.Combine(dir.Root, DatabaseFileName);
        SqliteIndexWriter.Write(db, [Doc("/old.html", "Old", "stale content")], true);
        SqliteIndexWriter.Write(db, [Doc("/new.html", "New", "fresh content")], true);
        var hits = QueryUrlsRanked(db, "content");
        await Assert.That(hits).Contains("/new.html");
        await Assert.That(hits).DoesNotContain("/old.html");
    }

    /// <summary>Two builds of the same corpus produce byte-identical databases.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task DeterministicOutput()
    {
        using TempDir dir = new();
        var a = Path.Combine(dir.Root, "a.db");
        var b = Path.Combine(dir.Root, "b.db");
        SearchDocument[] corpus =
            [Doc("/one.html", "One", "first page body"), Doc("/two.html", "Two", "second page body")];
        SqliteIndexWriter.Write(a, corpus, true);
        SqliteIndexWriter.Write(b, corpus, true);
        var bytesA = await File.ReadAllBytesAsync(a);
        var bytesB = await File.ReadAllBytesAsync(b);
        await Assert.That(bytesA.SequenceEqual(bytesB)).IsTrue();
    }

    /// <summary>Builds a <see cref="SearchDocument"/> from UTF-8-encoded fields.</summary>
    /// <param name="url">Root-relative URL.</param>
    /// <param name="title">Page title.</param>
    /// <param name="body">Body text.</param>
    /// <returns>The document.</returns>
    private static SearchDocument Doc(string url, string title, string body) =>
        new(Encoding.UTF8.GetBytes(url), Encoding.UTF8.GetBytes(title), Encoding.UTF8.GetBytes(body));

    /// <summary>Opens the database read-only and returns the URLs matching <paramref name="ftsQuery"/>, ranked by bm25.</summary>
    /// <param name="dbPath">Absolute path to the database.</param>
    /// <param name="ftsQuery">FTS5 MATCH expression.</param>
    /// <returns>Matching URLs in rank order.</returns>
    /// <exception cref="InvalidOperationException">The database could not be opened.</exception>
    private static List<string> QueryUrlsRanked(string dbPath, string ftsQuery)
    {
        Batteries_V2.Init();
        var rc = raw.sqlite3_open_v2(dbPath, out var db, raw.SQLITE_OPEN_READONLY, null);
        if (rc != raw.SQLITE_OK)
        {
            throw new InvalidOperationException($"open failed: {rc}");
        }

        try
        {
            _ = raw.sqlite3_prepare_v2(
                db,
                "SELECT url FROM pages WHERE pages MATCH ? ORDER BY bm25(pages, 10.0, 1.0)",
                out var stmt);
            try
            {
                _ = raw.sqlite3_bind_text(stmt, 1, ftsQuery);
                var urls = new List<string>();
                while (raw.sqlite3_step(stmt) == raw.SQLITE_ROW)
                {
                    urls.Add(raw.sqlite3_column_text(stmt, 0).utf8_to_string());
                }

                return urls;
            }
            finally
            {
                _ = raw.sqlite3_finalize(stmt);
            }
        }
        finally
        {
            _ = raw.sqlite3_close_v2(db);
        }
    }

    /// <summary>Returns the stored UTF-8 byte length of the <c>body</c> column for <paramref name="url"/>, or -1 when absent.</summary>
    /// <param name="dbPath">Absolute path to the database.</param>
    /// <param name="url">Row URL to look up.</param>
    /// <returns>Stored body length in bytes, or -1.</returns>
    private static int StoredBodyLength(string dbPath, string url)
    {
        Batteries_V2.Init();
        _ = raw.sqlite3_open_v2(dbPath, out var db, raw.SQLITE_OPEN_READONLY, null);
        try
        {
            _ = raw.sqlite3_prepare_v2(db, "SELECT body FROM pages WHERE url = ?", out var stmt);
            try
            {
                _ = raw.sqlite3_bind_text(stmt, 1, url);
                return raw.sqlite3_step(stmt) != raw.SQLITE_ROW
                    ? -1
                    : Encoding.UTF8.GetByteCount(raw.sqlite3_column_text(stmt, 0).utf8_to_string());
            }
            finally
            {
                _ = raw.sqlite3_finalize(stmt);
            }
        }
        finally
        {
            _ = raw.sqlite3_close_v2(db);
        }
    }
}
