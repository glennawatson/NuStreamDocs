// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Autorefs.Logging;

/// <summary>
/// Source-generated logging entry points for <see cref="AutorefsPlugin"/>
/// and <see cref="AutorefsRewriter"/>.
/// </summary>
/// <remarks>
/// All methods are <see cref="LoggerMessageAttribute"/> partials. Debug-
/// level entries that bind expensive arguments should be invoked via
/// <c>NuStreamDocs.Logging.LogInvokerHelper.Invoke</c> at the call site
/// so the projection only runs when the level is enabled.
/// </remarks>
internal static partial class AutorefsLoggingHelper
{
    /// <summary>Logs the start of a resolution pass.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="catalogSize">Number of registered IDs at the start of the pass.</param>
    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Autorefs resolution pass starting ({CatalogSize} registered ID(s))")]
    public static partial void LogResolutionStart(ILogger logger, int catalogSize);

    /// <summary>Logs a single resolved reference at debug level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="id">Resolved ID.</param>
    /// <param name="targetPath">Resolved target URL or file path.</param>
    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Debug,
        Message = "Autorefs resolved {Id} -> {TargetPath}")]
    public static partial void LogReferenceResolved(ILogger logger, string id, string targetPath);

    /// <summary>Logs an unresolved reference at warning level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="id">Unresolved ID.</param>
    /// <param name="sourcePage">Page that referenced the ID.</param>
    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Warning,
        Message = "Autorefs could not resolve {Id} on page {SourcePage}")]
    public static partial void LogReferenceUnresolved(ILogger logger, string id, string sourcePage);

    /// <summary>Logs the end-of-pass summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="resolvedCount">Number of references successfully resolved.</param>
    /// <param name="missingCount">Number of references that failed to resolve.</param>
    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Information,
        Message = "Autorefs resolution complete: {ResolvedCount} resolved, {MissingCount} missing")]
    public static partial void LogResolutionComplete(ILogger logger, int resolvedCount, int missingCount);
}
