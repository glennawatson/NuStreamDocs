// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Versions.Logging;

/// <summary>
/// Source-generated logging helpers for the versions plugin.
/// </summary>
internal static partial class VersionsLoggingHelper
{
    /// <summary>Logs a manifest read.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Manifest path.</param>
    /// <param name="versionCount">Versions discovered in the manifest.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "Read versions manifest from {Path}: {VersionCount} version(s)")]
    public static partial void LogManifestRead(ILogger logger, string path, int versionCount);

    /// <summary>Logs a manifest write.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Manifest path.</param>
    /// <param name="versionCount">Versions written to the manifest.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Wrote versions manifest to {Path}: {VersionCount} version(s)")]
    public static partial void LogManifestWrite(ILogger logger, string path, int versionCount);
}
