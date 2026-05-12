// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Fonts.Logging;

/// <summary>Source-generated log messages for the fonts pipeline.</summary>
internal static partial class FontsLogging
{
    /// <summary>Logs that a declared family was resolved into a set of font files.</summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="family">CSS family name.</param>
    /// <param name="fileCount">Number of resolved font files.</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Resolved font family '{family}' ({fileCount} files)")]
    public static partial void LogFontResolved(ILogger logger, string family, int fileCount);

    /// <summary>Logs that no metrics could be read for a family, so its CLS fallback override was skipped.</summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="family">CSS family name.</param>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Could not read metrics for font family '{family}'; emitting it without a CLS fallback override")]
    public static partial void LogMetricsUnavailable(ILogger logger, string family);

    /// <summary>Logs the generated stylesheet and the total number of embedded font files.</summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="path">Site-relative path to <c>fonts.css</c>.</param>
    /// <param name="fileCount">Total number of font files written.</param>
    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Wrote {path} ({fileCount} font files)")]
    public static partial void LogStylesheetWritten(ILogger logger, string path, int fileCount);
}
