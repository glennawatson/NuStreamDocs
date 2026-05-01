// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Bibliography.Logging;

/// <summary>Source-generated logging entry points for the bibliography plugin.</summary>
internal static partial class BibliographyLoggingHelper
{
    /// <summary>Logs a single unresolved <c>[@key]</c>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="key">Unresolved citation key.</param>
    [LoggerMessage(
        EventId = 9101,
        Level = LogLevel.Warning,
        Message = "Bibliography: no entry for [@{Key}]; left in place")]
    public static partial void LogMissingCitation(ILogger logger, string key);
}
