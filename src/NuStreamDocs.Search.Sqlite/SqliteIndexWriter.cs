// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using SQLitePCL;

namespace NuStreamDocs.Search.Sqlite;

/// <summary>Builds the single <c>search.db</c> SQLite/FTS5 index consumed in the browser via sql.js-httpvfs.</summary>
public static class SqliteIndexWriter
{
    /// <summary>
    /// FTS5 schema: <c>url</c> is unindexed (returned verbatim); <c>title</c> is boosted via bm25; <c>body</c> carries the searchable
    /// text. The prefix index covers 2- and 3-character queries.
    /// </summary>
    private const string CreateTableSql =
        "CREATE VIRTUAL TABLE pages USING fts5(url UNINDEXED, title, body, tokenize = 'unicode61 remove_diacritics 2', prefix = '2 3');";

    /// <summary>INSERT statement bound once per document.</summary>
    private const string InsertSql = "INSERT INTO pages(url, title, body) VALUES(?, ?, ?);";

    /// <summary>One-based bind index of the <c>url</c> column.</summary>
    private const int UrlColumn = 1;

    /// <summary>One-based bind index of the <c>title</c> column.</summary>
    private const int TitleColumn = 2;

    /// <summary>One-based bind index of the <c>body</c> column.</summary>
    private const int BodyColumn = 3;

    /// <summary>Initializes static members of the <see cref="SqliteIndexWriter"/> class — wires up the bundled <c>e_sqlite3</c> provider.</summary>
    static SqliteIndexWriter() => Batteries_V2.Init();

    /// <summary>Writes <paramref name="documents"/> to a freshly built FTS5 database at <paramref name="path"/>.</summary>
    /// <param name="path">Absolute output path for <c>search.db</c>; an existing file is replaced.</param>
    /// <param name="documents">Document corpus, already filtered and ordered by the caller.</param>
    /// <param name="indexFullBody">When true the whole body text is stored; when false only a short leading excerpt (see <see cref="SqliteOptions.ExcerptByteLimit"/>).</param>
    public static void Write(in FilePath path, SearchDocument[] documents, bool indexFullBody)
    {
        ArgumentException.ThrowIfNullOrEmpty(path.Value);
        ArgumentNullException.ThrowIfNull(documents);

        if (File.Exists(path.Value))
        {
            File.Delete(path.Value);
        }

        Check(
            raw.sqlite3_open_v2(path.Value, out var db, raw.SQLITE_OPEN_READWRITE | raw.SQLITE_OPEN_CREATE, null),
            db,
            "open");
        try
        {
            Run(db, "PRAGMA journal_mode=MEMORY;");
            Run(db, "PRAGMA synchronous=OFF;");
            Run(db, CreateTableSql);
            Run(db, "BEGIN;");
            InsertAll(db, documents, indexFullBody);
            Run(db, "COMMIT;");
            Run(db, "INSERT INTO pages(pages) VALUES('optimize');");
            Run(db, "VACUUM;");
        }
        finally
        {
            raw.sqlite3_close_v2(db);
        }
    }

    /// <summary>Binds and steps the INSERT statement once per document, inside the caller's transaction.</summary>
    /// <param name="db">Open database handle.</param>
    /// <param name="documents">Document corpus.</param>
    /// <param name="indexFullBody">When true the whole body text is stored; when false only a short leading excerpt.</param>
    private static void InsertAll(sqlite3 db, SearchDocument[] documents, bool indexFullBody)
    {
        Check(raw.sqlite3_prepare_v2(db, InsertSql, out var stmt), db, "prepare insert");
        try
        {
            var excerptLimit = SqliteOptions.ExcerptByteLimit;
            for (var i = 0; i < documents.Length; i++)
            {
                var doc = documents[i];
                BindText(stmt, UrlColumn, doc.RelativeUrl);
                BindText(stmt, TitleColumn, doc.Title);
                BindText(stmt, BodyColumn, indexFullBody ? doc.Text : Excerpt(doc.Text, excerptLimit));
                if (raw.sqlite3_step(stmt) != raw.SQLITE_DONE)
                {
                    throw Fail(db, "insert step");
                }

                raw.sqlite3_reset(stmt);
                raw.sqlite3_clear_bindings(stmt);
            }
        }
        finally
        {
            raw.sqlite3_finalize(stmt);
        }
    }

    /// <summary>Returns the longest UTF-8 prefix of <paramref name="text"/> not exceeding <paramref name="limit"/> bytes, trimmed back to the last whole token.</summary>
    /// <param name="text">Full body text bytes.</param>
    /// <param name="limit">Maximum length, in bytes, of the returned prefix.</param>
    /// <returns>The original array when it already fits, otherwise a trimmed copy.</returns>
    private static byte[] Excerpt(byte[] text, int limit)
    {
        if (text.Length <= limit)
        {
            return text;
        }

        var end = limit;
        while (end > 0 && text[end] is not ((byte)' ' or (byte)'\n' or (byte)'\t' or (byte)'\r'))
        {
            end--;
        }

        return end == 0 ? text[..limit] : text[..end];
    }

    /// <summary>Binds <paramref name="value"/> as UTF-8 text to bind index <paramref name="index"/> of <paramref name="stmt"/>.</summary>
    /// <param name="stmt">Prepared statement.</param>
    /// <param name="index">One-based bind index.</param>
    /// <param name="value">UTF-8 bytes to bind.</param>
    private static void BindText(sqlite3_stmt stmt, int index, byte[] value) =>
        raw.sqlite3_bind_text(stmt, index, (ReadOnlySpan<byte>)value);

    /// <summary>Runs a single SQL statement, throwing on a non-success result code.</summary>
    /// <param name="db">Open database handle.</param>
    /// <param name="sql">SQL text.</param>
    private static void Run(sqlite3 db, string sql) => Check(raw.sqlite3_exec(db, sql), db, sql);

    /// <summary>Throws an <see cref="InvalidOperationException"/> when <paramref name="rc"/> is not a success code.</summary>
    /// <param name="rc">Result code returned by a SQLite call.</param>
    /// <param name="db">Open database handle, used to read the error message.</param>
    /// <param name="what">Short label of the operation, included in the thrown message.</param>
    private static void Check(int rc, sqlite3 db, string what)
    {
        if (rc is raw.SQLITE_OK or raw.SQLITE_ROW or raw.SQLITE_DONE)
        {
            return;
        }

        throw Fail(db, what);
    }

    /// <summary>Builds the exception for a failed SQLite call.</summary>
    /// <param name="db">Open database handle, used to read the error message.</param>
    /// <param name="what">Short label of the operation that failed.</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidOperationException Fail(sqlite3 db, string what) =>
        new(StringCompose.Concat("SQLite error during '", what, "': ", raw.sqlite3_errmsg(db).utf8_to_string()));
}
