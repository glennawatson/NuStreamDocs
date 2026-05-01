// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Logging;

/// <summary>
/// Source-generated <see cref="ILogger"/> messages for the
/// <c>NuStreamDocs.Caching</c> namespace (manifest + bounded cache).
/// </summary>
internal static partial class CachingLoggingHelper
{
    /// <summary>Logs that a build manifest was read from disk.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Absolute manifest path.</param>
    /// <param name="entryCount">Number of entries loaded.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded build manifest from {Path} ({EntryCount} entry/entries)")]
    public static partial void LogManifestLoaded(ILogger logger, string path, int entryCount);

    /// <summary>Logs that a build manifest was written to disk.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="path">Absolute manifest path.</param>
    /// <param name="entryCount">Number of entries persisted.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Saved build manifest to {Path} ({EntryCount} entry/entries)")]
    public static partial void LogManifestSaved(ILogger logger, string path, int entryCount);

    /// <summary>Logs a bounded-cache eviction event at debug level.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="reason">Eviction reason (e.g. "capacity", "compact").</param>
    /// <param name="removedCount">Number of entries removed in this sweep.</param>
    /// <param name="remaining">Entries still in the cache after the sweep.</param>
    [LoggerMessage(Level = LogLevel.Debug, Message = "BoundedCache evicted {RemovedCount} entry/entries ({Reason}); {Remaining} remaining")]
    public static partial void LogCacheEviction(ILogger logger, string reason, int removedCount, int remaining);
}
