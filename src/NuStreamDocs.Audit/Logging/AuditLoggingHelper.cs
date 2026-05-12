// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Audit.Logging;

/// <summary>Source-generated logging helpers for the audit plugin.</summary>
internal static partial class AuditLoggingHelper
{
    /// <summary>Logs the start of an audit run.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="outputRoot">Site output root being audited.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Accessibility/performance audit starting at {OutputRoot}")]
    public static partial void LogAuditStart(ILogger logger, string outputRoot);

    /// <summary>Logs one audit finding.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="page">Page the finding came from.</param>
    /// <param name="rule">The lint that fired.</param>
    /// <param name="message">Diagnostic message.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "{Page}: {Rule}: {Message}")]
    public static partial void LogAuditFinding(ILogger logger, string page, AuditRule rule, string message);

    /// <summary>Logs the end-of-run summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pageCount">Pages audited.</param>
    /// <param name="findingCount">Findings raised.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Audit complete: {FindingCount} finding(s) across {PageCount} page(s)")]
    public static partial void LogAuditComplete(ILogger logger, int pageCount, int findingCount);
}
