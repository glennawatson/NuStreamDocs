// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Search.Logging;

/// <summary>
/// Source-generated logging helpers for the search-index plugin.
/// </summary>
internal static partial class SearchLoggingHelper
{
    /// <summary>Logs the start of an index build.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="documentCount">Documents harvested from the rendered pages.</param>
    /// <param name="format">Search backend (Pagefind / Lunr).</param>
    /// <param name="outputDir">Search subdirectory under the site output root.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Building {Format} search index from {DocumentCount} document(s); output {OutputDir}")]
    public static partial void LogIndexBuildStart(ILogger logger, int documentCount, SearchFormat format, string outputDir);

    /// <summary>Logs the end of an index build.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="recordCount">Records written to the index.</param>
    /// <param name="totalContentBytes">Total UTF-8 byte length of the indexed text.</param>
    /// <param name="outputDir">Search subdirectory under the site output root.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Search index complete: {RecordCount} record(s), {TotalContentBytes} byte(s) of content written to {OutputDir}")]
    public static partial void LogIndexBuildComplete(ILogger logger, int recordCount, long totalContentBytes, string outputDir);

    /// <summary>Logs one harvested document at debug.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="slug">Site-relative URL of the document.</param>
    /// <param name="contentLength">UTF-8 byte length of the indexed text.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexed document {Slug} ({ContentLength} byte(s))")]
    public static partial void LogDocumentIndexed(ILogger logger, string slug, int contentLength);
}
