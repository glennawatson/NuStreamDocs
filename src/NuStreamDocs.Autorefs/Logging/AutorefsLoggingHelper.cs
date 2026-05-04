// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Text;
using NuStreamDocs.Common;

namespace NuStreamDocs.Autorefs.Logging;

/// <summary>
/// Source-generated logging entry points for <see cref="AutorefsPlugin"/>
/// and <see cref="AutorefsRewriter"/>.
/// </summary>
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

    /// <summary>Logs a resolved reference.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="urlBytes">The url bytes.</param>
    /// <param name="id">UTF-8 reference ID bytes.</param>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "False positive: registry lookup is gated on logger.IsEnabled.")]
    public static void LogReferenceResolved(ILogger? logger, ReadOnlySpan<byte> urlBytes, ReadOnlySpan<byte> id)
    {
        if (logger is null)
        {
            return;
        }

        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        LogReferenceResolvedCore(logger, Encoding.UTF8.GetString(id), Encoding.UTF8.GetString(urlBytes));
    }

    /// <summary>Logs an unresolved reference.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="id">UTF-8 unresolved ID bytes.</param>
    /// <param name="sourcePage">Page that referenced the ID.</param>
    public static void LogReferenceUnresolved(ILogger logger, ReadOnlySpan<byte> id, FilePath sourcePage)
    {
        if (!logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        LogReferenceUnresolvedCore(logger, Encoding.UTF8.GetString(id), sourcePage);
    }

    /// <summary>Logs the end-of-pass summary.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="resolvedCount">Number of references successfully resolved.</param>
    /// <param name="missingCount">Number of references that failed to resolve.</param>
    [LoggerMessage(
        EventId = 5004,
        Level = LogLevel.Information,
        Message = "Autorefs resolution complete: {ResolvedCount} resolved, {MissingCount} missing")]
    public static partial void LogResolutionComplete(ILogger logger, int resolvedCount, int missingCount);

    /// <summary>Source-generated emitter for <see cref="LogReferenceResolved"/>; takes the already-decoded strings.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="id">Resolved ID.</param>
    /// <param name="targetPath">Resolved target URL or file path.</param>
    [LoggerMessage(
        EventId = 5002,
        Level = LogLevel.Debug,
        Message = "Autorefs resolved {Id} -> {TargetPath}")]
    private static partial void LogReferenceResolvedCore(ILogger logger, string id, string targetPath);

    /// <summary>Source-generated emitter for <see cref="LogReferenceUnresolved"/>; takes the already-decoded strings.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="id">Unresolved ID.</param>
    /// <param name="sourcePage">Page that referenced the ID.</param>
    [LoggerMessage(
        EventId = 5003,
        Level = LogLevel.Warning,
        Message = "Autorefs could not resolve {Id} on page {SourcePage}")]
    private static partial void LogReferenceUnresolvedCore(ILogger logger, string id, string sourcePage);
}
