// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Redirects.Logging;

/// <summary>Source-generated log messages for the redirects plugin.</summary>
internal static partial class RedirectsLogging
{
    /// <summary>Logs the artifacts written.</summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="redirectCount">Number of redirects written.</param>
    /// <param name="headerRuleCount">Number of <c>_headers</c> rule blocks written.</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information,
        Message = "Wrote {redirectCount} redirects and {headerRuleCount} header rules")]
    public static partial void LogWritten(ILogger logger, int redirectCount, int headerRuleCount);

    /// <summary>Logs that a meta-refresh page was skipped because a real page already occupies its path.</summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="path">The redirect source path.</param>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message =
            "Redirect source '{path}' is already a rendered page; emitting the _redirects entry but not a meta-refresh HTML page there")]
    public static partial void LogSkippedClobber(ILogger logger, string path);

    /// <summary>Logs that a malformed redirect was ignored.</summary>
    /// <param name="logger">Diagnostic logger.</param>
    /// <param name="from">The (possibly empty) redirect source.</param>
    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Ignored a redirect with an empty source or destination (source='{from}')")]
    public static partial void LogIgnoredRedirect(ILogger logger, string from);
}
