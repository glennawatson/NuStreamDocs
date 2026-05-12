// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.ContentLoader.Logging;

/// <summary>Source-generated logging helpers for content loaders.</summary>
internal static partial class ContentLoaderLoggingHelper
{
    /// <summary>Logs how many pages a loader produced.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="loader">Loader name.</param>
    /// <param name="pageCount">Pages produced.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Content loader '{Loader}' produced {PageCount} page(s)")]
    public static partial void LogLoaderProduced(ILogger logger, string loader, int pageCount);

    /// <summary>Logs that the configured collection pointer did not resolve to a JSON array.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="loader">Loader name.</param>
    /// <param name="pointer">The dotted collection pointer.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Content loader '{Loader}': collection pointer '{Pointer}' did not resolve to a JSON array; no pages produced")]
    public static partial void LogCollectionPointerMissed(ILogger logger, string loader, string pointer);

    /// <summary>Logs that an entry was skipped because a route-template field was missing or non-scalar.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="loader">Loader name.</param>
    /// <param name="template">The route template.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Content loader '{Loader}': skipped an entry — route template '{Template}' references a missing or non-scalar field")]
    public static partial void LogSkippedEntry(ILogger logger, string loader, string template);

    /// <summary>Logs a fetch failure.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="loader">Loader name.</param>
    /// <param name="url">The URL that failed.</param>
    /// <param name="reason">Failure reason.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Content loader '{Loader}': fetch of {Url} failed — {Reason}")]
    public static partial void LogFetchFailed(ILogger logger, string loader, string url, string reason);
}
