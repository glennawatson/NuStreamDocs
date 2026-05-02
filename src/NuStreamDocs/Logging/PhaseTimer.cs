// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace NuStreamDocs.Logging;

/// <summary>Runs a unit of work bracketed by paired "starting" / "completed" log entries with the elapsed time in seconds.</summary>
/// <remarks>
/// The start log fires immediately, the work runs, the end log fires from a
/// <c>finally</c> block — so even when the work throws, the elapsed-time
/// entry is still emitted with the time spent before the exception
/// propagated. Both log delegates are typically the source-generated
/// partial methods on a per-feature <c>*LoggingHelper</c>; the closure
/// captures the per-iteration parameters (plugin name, file path, batch
/// size) so the helper itself stays generic over what's being measured.
/// </remarks>
public static class PhaseTimer
{
    /// <summary>Runs <paramref name="action"/> bracketed by the start / complete log entries; elapsed time covers the entire call.</summary>
    /// <param name="logger">Logger forwarded to both delegates.</param>
    /// <param name="logStart">Invoked immediately, before <paramref name="action"/> runs.</param>
    /// <param name="logComplete">Invoked from a <c>finally</c> block with the elapsed wall-clock time in seconds.</param>
    /// <param name="action">The work to time.</param>
    /// <returns>A task that completes when <paramref name="action"/> completes.</returns>
    public static async ValueTask RunAsync(
        ILogger logger,
        Action<ILogger> logStart,
        Action<ILogger, double> logComplete,
        Func<ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(logStart);
        ArgumentNullException.ThrowIfNull(logComplete);
        ArgumentNullException.ThrowIfNull(action);

        logStart(logger);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            logComplete(logger, stopwatch.Elapsed.TotalSeconds);
        }
    }

    /// <summary>Async overload returning the work's result; the completion log receives the result so it can include result-derived counts (page totals, broken-link counts, …).</summary>
    /// <typeparam name="TResult">Result type produced by <paramref name="action"/>.</typeparam>
    /// <param name="logger">Logger forwarded to both delegates.</param>
    /// <param name="logStart">Start log entry.</param>
    /// <param name="logComplete">Completion log entry; receives the result and elapsed seconds. Fires only on success.</param>
    /// <remarks>When <paramref name="action"/> throws, the start log is the only marker and the exception propagates without firing <paramref name="logComplete"/>.</remarks>
    /// <param name="action">The work to time.</param>
    /// <returns>The value produced by <paramref name="action"/>.</returns>
    public static async ValueTask<TResult> RunAsync<TResult>(
        ILogger logger,
        Action<ILogger> logStart,
        Action<ILogger, TResult, double> logComplete,
        Func<ValueTask<TResult>> action)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(logStart);
        ArgumentNullException.ThrowIfNull(logComplete);
        ArgumentNullException.ThrowIfNull(action);

        logStart(logger);
        var stopwatch = Stopwatch.StartNew();
        var result = await action().ConfigureAwait(false);
        stopwatch.Stop();
        logComplete(logger, result, stopwatch.Elapsed.TotalSeconds);
        return result;
    }

    /// <summary>Synchronous overload — same start / end semantics for non-async work.</summary>
    /// <param name="logger">Logger forwarded to both delegates.</param>
    /// <param name="logStart">Start log entry.</param>
    /// <param name="logComplete">Completion log entry; receives elapsed seconds.</param>
    /// <param name="action">The work to time.</param>
    public static void Run(
        ILogger logger,
        Action<ILogger> logStart,
        Action<ILogger, double> logComplete,
        Action action)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(logStart);
        ArgumentNullException.ThrowIfNull(logComplete);
        ArgumentNullException.ThrowIfNull(action);

        logStart(logger);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            stopwatch.Stop();
            logComplete(logger, stopwatch.Elapsed.TotalSeconds);
        }
    }
}
