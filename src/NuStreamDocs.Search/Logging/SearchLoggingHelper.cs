// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Logging;

/// <summary>
/// Source-generated logging helpers for the search-index plugin.
/// </summary>
/// <remarks>
/// The public entry points self-gate on <see cref="ILogger.IsEnabled(LogLevel)"/>
/// so call sites can hand <see cref="DirectoryPath"/> values and UTF-8 byte
/// spans straight in without their own gating dance. The private <c>*Core</c>
/// partials are the <c>[LoggerMessage]</c> source-gen targets and require
/// strings.
/// </remarks>
internal static partial class SearchLoggingHelper
{
    /// <summary>Logs the start of an index build at information level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="documentCount">Documents harvested from the rendered pages.</param>
    /// <param name="format">Search backend (Pagefind / Lunr).</param>
    /// <param name="outputDir">Search subdirectory under the site output root.</param>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "False positive: implicit DirectoryPath-to-string conversion is gated on logger.IsEnabled.")]
    public static void LogIndexBuildStart(ILogger logger, int documentCount, SearchFormat format, DirectoryPath outputDir)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        LogIndexBuildStartCore(logger, documentCount, format, outputDir);
    }

    /// <summary>Logs the end of an index build at information level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="recordCount">Records written to the index.</param>
    /// <param name="totalContentBytes">Total UTF-8 byte length of the indexed text.</param>
    /// <param name="outputDir">Search subdirectory under the site output root.</param>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "False positive: implicit DirectoryPath-to-string conversion is gated on logger.IsEnabled.")]
    public static void LogIndexBuildComplete(ILogger logger, int recordCount, long totalContentBytes, DirectoryPath outputDir)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        LogIndexBuildCompleteCore(logger, recordCount, totalContentBytes, outputDir);
    }

    /// <summary>Logs one harvested document at debug level — UTF-8 decode of the slug is deferred until the level is enabled.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="slug">UTF-8 site-relative URL bytes for the document.</param>
    /// <param name="contentLength">UTF-8 byte length of the indexed text.</param>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "False positive: GetString call is gated on logger.IsEnabled above.")]
    public static void LogDocumentIndexed(ILogger logger, ReadOnlySpan<byte> slug, int contentLength)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        LogDocumentIndexedCore(logger, Encoding.UTF8.GetString(slug), contentLength);
    }

    /// <summary>Source-generated emitter for <see cref="LogIndexBuildStart"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="documentCount">Documents harvested from the rendered pages.</param>
    /// <param name="format">Search backend (Pagefind / Lunr).</param>
    /// <param name="outputDir">Search subdirectory under the site output root.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Building {Format} search index from {DocumentCount} document(s); output {OutputDir}")]
    private static partial void LogIndexBuildStartCore(ILogger logger, int documentCount, SearchFormat format, string outputDir);

    /// <summary>Source-generated emitter for <see cref="LogIndexBuildComplete"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="recordCount">Records written to the index.</param>
    /// <param name="totalContentBytes">Total UTF-8 byte length of the indexed text.</param>
    /// <param name="outputDir">Search subdirectory under the site output root.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Search index complete: {RecordCount} record(s), {TotalContentBytes} byte(s) of content written to {OutputDir}")]
    private static partial void LogIndexBuildCompleteCore(ILogger logger, int recordCount, long totalContentBytes, string outputDir);

    /// <summary>Source-generated emitter for <see cref="LogDocumentIndexed"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="slug">Site-relative URL of the document.</param>
    /// <param name="contentLength">UTF-8 byte length of the indexed text.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Indexed document {Slug} ({ContentLength} byte(s))")]
    private static partial void LogDocumentIndexedCore(ILogger logger, string slug, int contentLength);
}
