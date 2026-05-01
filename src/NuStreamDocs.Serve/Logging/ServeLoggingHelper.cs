// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Serve.Logging;

/// <summary>Source-generated logging entry points for the watch / dev-server pipeline.</summary>
internal static partial class ServeLoggingHelper
{
    /// <summary>Logs initial build completion + first-server-listen.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="url">Bound URL.</param>
    /// <param name="inputRoot">Watched input root.</param>
    /// <param name="outputRoot">Served output root.</param>
    [LoggerMessage(
        EventId = 8001,
        Level = LogLevel.Information,
        Message = "Dev server listening on {Url} — watching {InputRoot}, serving {OutputRoot}")]
    public static partial void LogServerStart(ILogger logger, string url, string inputRoot, string outputRoot);

    /// <summary>Logs a rebuild trigger after debounced file-system events.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="changeCount">Number of distinct paths that changed in the debounce window.</param>
    [LoggerMessage(
        EventId = 8002,
        Level = LogLevel.Information,
        Message = "Rebuilding after {ChangeCount} change(s)…")]
    public static partial void LogRebuildStart(ILogger logger, int changeCount);

    /// <summary>Logs a successful rebuild + reload signal.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="elapsedMs">Rebuild duration.</param>
    /// <param name="connectedClients">Number of websocket clients that received a reload.</param>
    [LoggerMessage(
        EventId = 8003,
        Level = LogLevel.Information,
        Message = "Rebuilt in {ElapsedMs} ms; signalled {ConnectedClients} connected client(s)")]
    public static partial void LogRebuildComplete(ILogger logger, long elapsedMs, int connectedClients);

    /// <summary>Logs a failed rebuild — the server stays up, the next save retries.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="exception">The exception thrown by the build pipeline.</param>
    [LoggerMessage(
        EventId = 8004,
        Level = LogLevel.Error,
        Message = "Rebuild failed; dev server stays up — fix the error and save again")]
    public static partial void LogRebuildFailed(ILogger logger, Exception exception);

    /// <summary>Logs a single file-system event accepted into the debounce window.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="changeType">FileSystemWatcher change kind.</param>
    /// <param name="path">Source-relative path that changed.</param>
    [LoggerMessage(
        EventId = 8005,
        Level = LogLevel.Debug,
        Message = "Watch event: {ChangeType} {Path}")]
    public static partial void LogWatchEvent(ILogger logger, WatcherChangeTypes changeType, string path);

    /// <summary>Logs a watcher error event.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="exception">Underlying exception from the watcher.</param>
    [LoggerMessage(
        EventId = 8007,
        Level = LogLevel.Warning,
        Message = "Watcher error — the FileSystemWatcher self-recovered")]
    public static partial void LogWatchError(ILogger logger, Exception exception);

    /// <summary>Logs server shutdown initiated by the host (Ctrl-C / cancellation).</summary>
    /// <param name="logger">Target logger.</param>
    [LoggerMessage(
        EventId = 8006,
        Level = LogLevel.Information,
        Message = "Dev server shutting down")]
    public static partial void LogServerStopping(ILogger logger);
}
