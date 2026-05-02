// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Optimize.Logging;

/// <summary>
/// Source-generated logging helpers for the optimize plugin.
/// </summary>
internal static partial class OptimizeLoggingHelper
{
    /// <summary>Logs the start of an optimize pass.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="fileCount">Files discovered as eligible for compression.</param>
    /// <param name="outputRoot">Site output root.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Optimize starting: {FileCount} file(s) under {OutputRoot}")]
    public static partial void LogOptimizeStart(ILogger logger, int fileCount, string outputRoot);

    /// <summary>Logs a successfully processed file.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Absolute path of the source file.</param>
    /// <param name="originalBytes">Source-file size in bytes.</param>
    /// <param name="compressedBytes">Compressed sibling size in bytes.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Optimize processed {Path}: {OriginalBytes} -> {CompressedBytes} bytes")]
    public static partial void LogFileProcessed(ILogger logger, string path, long originalBytes, long compressedBytes);

    /// <summary>Logs a skipped file with a reason.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Absolute path of the skipped file.</param>
    /// <param name="reason">Why the file was skipped.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Optimize skipped {Path}: {Reason}")]
    public static partial void LogFileSkipped(ILogger logger, string path, string reason);

    /// <summary>Logs the end-of-pass summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="filesProcessed">Files compressed.</param>
    /// <param name="bytesSaved">Total bytes saved (sum of original minus compressed across formats).</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Optimize complete: {FilesProcessed} file(s) processed, {BytesSaved} byte(s) saved")]
    public static partial void LogOptimizeComplete(ILogger logger, int filesProcessed, long bytesSaved);
}
