// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Common;

namespace NuStreamDocs.Versions.Logging;

/// <summary>Source-generated logging helpers for the versions plugin.</summary>
internal static partial class VersionsLoggingHelper
{
    /// <summary>Logs a manifest read at debug level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Manifest path.</param>
    /// <param name="versionCount">Versions discovered in the manifest.</param>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "False positive: implicit DirectoryPath-to-string conversion is gated on logger.IsEnabled.")]
    public static void LogManifestRead(ILogger logger, DirectoryPath path, int versionCount)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        LogManifestReadCore(logger, path, versionCount);
    }

    /// <summary>Logs a manifest write at information level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Manifest path.</param>
    /// <param name="versionCount">Versions written to the manifest.</param>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "False positive: implicit DirectoryPath-to-string conversion is gated on logger.IsEnabled.")]
    public static void LogManifestWrite(ILogger logger, DirectoryPath path, int versionCount)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        LogManifestWriteCore(logger, path, versionCount);
    }

    /// <summary>Source-generated emitter for <see cref="LogManifestRead"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Manifest path.</param>
    /// <param name="versionCount">Versions discovered in the manifest.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Read versions manifest from {Path}: {VersionCount} version(s)")]
    private static partial void LogManifestReadCore(ILogger logger, string path, int versionCount);

    /// <summary>Source-generated emitter for <see cref="LogManifestWrite"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Manifest path.</param>
    /// <param name="versionCount">Versions written to the manifest.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Wrote versions manifest to {Path}: {VersionCount} version(s)")]
    private static partial void LogManifestWriteCore(ILogger logger, string path, int versionCount);
}
