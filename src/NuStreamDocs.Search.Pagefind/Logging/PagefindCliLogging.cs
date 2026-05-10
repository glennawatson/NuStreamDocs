// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using NuStreamDocs.Common;

namespace NuStreamDocs.Search.Pagefind.Logging;

/// <summary>Source-generated logging helpers for <see cref="PagefindCli"/>.</summary>
internal static partial class PagefindCliLogging
{
    /// <summary>Logs that no Pagefind binary could be located for the host RID.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="rid">Host runtime identifier (e.g. <c>linux-x64</c>).</param>
    public static void LogBinaryMissing(ILogger logger, string rid)
    {
        if (!logger.IsEnabled(LogLevel.Warning))
        {
            return;
        }

        LogBinaryMissingCore(logger, rid);
    }

    /// <summary>Logs the Pagefind invocation about to run.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="binary">Resolved binary path.</param>
    /// <param name="siteRoot">Rendered site directory.</param>
    [SuppressMessage("Performance", "CA1873:Avoid potentially expensive logging", Justification = "False positive: implicit DirectoryPath-to-string conversion is gated on logger.IsEnabled.")]
    public static void LogInvoking(ILogger logger, string binary, in DirectoryPath siteRoot)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        LogInvokingCore(logger, binary, siteRoot);
    }

    /// <summary>Logs that the OS process couldn't be started.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="binary">Resolved binary path that failed to start.</param>
    /// <param name="reason">Exception message text from the start failure.</param>
    public static void LogStartFailed(ILogger logger, string binary, string reason)
    {
        if (!logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        LogStartFailedCore(logger, binary, reason);
    }

    /// <summary>Logs that Pagefind exited with a non-zero status.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="exitCode">Process exit code.</param>
    /// <param name="stderr">Captured stderr text.</param>
    public static void LogFailed(ILogger logger, int exitCode, string stderr)
    {
        if (!logger.IsEnabled(LogLevel.Error))
        {
            return;
        }

        LogFailedCore(logger, exitCode, stderr);
    }

    /// <summary>Logs successful invocation.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="stdoutBytes">Captured stdout byte length.</param>
    /// <param name="stderrBytes">Captured stderr byte length.</param>
    public static void LogSucceeded(ILogger logger, int stdoutBytes, int stderrBytes)
    {
        if (!logger.IsEnabled(LogLevel.Information))
        {
            return;
        }

        LogSucceededCore(logger, stdoutBytes, stderrBytes);
    }

    /// <summary>Source-generated emitter for <see cref="LogBinaryMissing"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="rid">Host runtime identifier.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Pagefind CLI binary not found for RID {Rid}; search shards will not be generated.")]
    private static partial void LogBinaryMissingCore(ILogger logger, string rid);

    /// <summary>Source-generated emitter for <see cref="LogInvoking"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="binary">Resolved binary path.</param>
    /// <param name="siteRoot">Rendered site directory.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Invoking Pagefind {Binary} against {SiteRoot}")]
    private static partial void LogInvokingCore(ILogger logger, string binary, string siteRoot);

    /// <summary>Source-generated emitter for <see cref="LogStartFailed"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="binary">Binary path that failed to start.</param>
    /// <param name="reason">Failure message.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to start Pagefind process at {Binary}: {Reason}")]
    private static partial void LogStartFailedCore(ILogger logger, string binary, string reason);

    /// <summary>Source-generated emitter for <see cref="LogFailed"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="exitCode">Process exit code.</param>
    /// <param name="stderr">Captured stderr.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Pagefind exited with code {ExitCode}. stderr: {Stderr}")]
    private static partial void LogFailedCore(ILogger logger, int exitCode, string stderr);

    /// <summary>Source-generated emitter for <see cref="LogSucceeded"/>.</summary>
    /// <param name="logger">Target logger.</param>
    /// <param name="stdoutBytes">Captured stdout byte length.</param>
    /// <param name="stderrBytes">Captured stderr byte length.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Pagefind succeeded ({StdoutBytes} byte(s) stdout, {StderrBytes} byte(s) stderr)")]
    private static partial void LogSucceededCore(ILogger logger, int stdoutBytes, int stderrBytes);
}
