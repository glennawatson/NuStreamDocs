// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Privacy.Logging;

/// <summary>
/// Source-generated logging helpers for the privacy plugin.
/// </summary>
internal static partial class PrivacyLoggingHelper
{
    /// <summary>Logs the start of asset localization.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="assetCount">Distinct external assets discovered.</param>
    /// <param name="outputRoot">Site output root.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Privacy localization starting: {AssetCount} external asset(s) for {OutputRoot}")]
    public static partial void LogLocalizationStart(ILogger logger, int assetCount, string outputRoot);

    /// <summary>Logs a successful per-asset download.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="url">External URL that was downloaded.</param>
    /// <param name="localPath">Local path the bytes landed at.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Privacy downloaded {Url} -> {LocalPath}")]
    public static partial void LogDownloadSuccess(ILogger logger, string url, string localPath);

    /// <summary>Logs a per-asset download failure.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="url">External URL that failed.</param>
    /// <param name="localPath">Local path that would have been written.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Privacy download failed for {Url} (target {LocalPath})")]
    public static partial void LogDownloadFailure(ILogger logger, string url, string localPath);

    /// <summary>Logs a cache hit during the privacy fetch loop.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="url">External URL whose cached copy was reused.</param>
    /// <param name="cachePath">Cache path the bytes were copied from.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Privacy cache hit for {Url} from {CachePath}")]
    public static partial void LogCacheHit(ILogger logger, string url, string cachePath);

    /// <summary>Logs the localization summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="localizedCount">Assets successfully written to the output root.</param>
    /// <param name="failedCount">Assets that exhausted retries.</param>
    /// <param name="cachedCount">Assets satisfied from the on-disk cache.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Privacy finalize: {LocalizedCount} localized, {FailedCount} failed, {CachedCount} cached")]
    public static partial void LogFinalizeSummary(ILogger logger, int localizedCount, int failedCount, int cachedCount);

    /// <summary>Logs an audit-mode manifest write.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="manifestPath">Path the audit manifest was written to.</param>
    /// <param name="urlCount">Number of URLs recorded.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Privacy audit manifest written to {ManifestPath} with {UrlCount} URL(s)")]
    public static partial void LogAuditWrite(ILogger logger, string manifestPath, int urlCount);
}
