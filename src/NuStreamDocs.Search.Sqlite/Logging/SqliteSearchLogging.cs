// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Sqlite.Logging;

/// <summary>Source-generated log messages for the SQLite search backend.</summary>
internal static partial class SqliteSearchLogging
{
    /// <summary>Logs the database that was written and its byte size.</summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="path">Absolute path to <c>search.db</c>.</param>
    /// <param name="bytes">File size in bytes.</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "SQLite search index written: {path} ({bytes} bytes)")]
    public static partial void LogDatabaseWritten(ILogger logger, string path, long bytes);
}
