// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Feed.Logging;

/// <summary>
/// Source-generated logging helpers for the feed plugin.
/// </summary>
internal static partial class FeedLoggingHelper
{
    /// <summary>Logs the start of a feed write.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="format">Feed format being written (RSS / Atom).</param>
    /// <param name="path">Absolute output path.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Writing {Format} feed to {Path}")]
    public static partial void LogFeedWriteStart(ILogger logger, string format, string path);

    /// <summary>Logs the end of a feed write.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="format">Feed format that was written.</param>
    /// <param name="entryCount">Number of entries written.</param>
    /// <param name="byteCount">Bytes written to disk.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "{Format} feed complete: {EntryCount} entry/entries, {ByteCount} byte(s)")]
    public static partial void LogFeedWriteComplete(ILogger logger, string format, int entryCount, int byteCount);
}
