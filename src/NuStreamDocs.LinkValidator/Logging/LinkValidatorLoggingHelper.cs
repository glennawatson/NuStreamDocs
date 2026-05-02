// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.LinkValidator.Logging;

/// <summary>
/// Source-generated logging helpers for the link validator plugin.
/// </summary>
internal static partial class LinkValidatorLoggingHelper
{
    /// <summary>Logs the start of a link validation run.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="outputRoot">Site output root being validated.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Link validation starting at {OutputRoot}")]
    public static partial void LogValidationStart(ILogger logger, string outputRoot);

    /// <summary>Logs the corpus-discovery summary emitted between the start log and the validators.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="pageCount">Pages discovered.</param>
    /// <param name="internalLinkCount">Internal links found.</param>
    /// <param name="externalLinkCount">External links found.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Validation corpus: {PageCount} page(s), {InternalLinkCount} internal link(s), {ExternalLinkCount} external link(s)")]
    public static partial void LogValidationCorpus(ILogger logger, int pageCount, int internalLinkCount, int externalLinkCount);

    /// <summary>Logs the end-of-run summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="brokenCount">Diagnostics raised at error severity.</param>
    /// <param name="warningCount">Diagnostics raised at warning severity.</param>
    /// <param name="elapsedSeconds">Total elapsed time in seconds (two decimal places).</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Link validation complete: {BrokenCount} broken, {WarningCount} warning(s) in {ElapsedSeconds:F2}s")]
    public static partial void LogValidationComplete(ILogger logger, int brokenCount, int warningCount, double elapsedSeconds);

    /// <summary>Logs a single broken-link diagnostic.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="severity">Diagnostic severity.</param>
    /// <param name="sourcePage">Page on which the broken link was found.</param>
    /// <param name="message">Diagnostic message.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "[{Severity}] {SourcePage}: {Message}")]
    public static partial void LogBrokenLink(ILogger logger, LinkSeverity severity, string sourcePage, string message);

    /// <summary>Logs an external rate-limit hit.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="host">Host whose rate limit was reached.</param>
    /// <param name="queueDepth">Number of requests queued behind the limiter.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "External rate limit hit for host {Host}; {QueueDepth} request(s) queued")]
    public static partial void LogRateLimitHit(ILogger logger, string host, int queueDepth);
}
