// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using NuStreamDocs.Common;

namespace NuStreamDocs.Logging;

/// <summary>Owns per-plugin timing for one build: measures hook invocations, accumulates the totals, and emits the end-of-build summary log.</summary>
/// <remarks>
/// One instance per <c>BuildPipeline.RunAsync</c> call. Each hook invocation enters a
/// <see cref="Measure"/> scope; <see cref="Emit"/> dumps the table at end-of-build, sorted by
/// total time descending. Plugins under <see cref="SignificantSeconds"/> drop to Debug so the
/// Info-level summary stays focused on the long-runners.
/// </remarks>
public sealed class PluginTimingTable
{
    /// <summary>Threshold (in seconds) under which an entry is logged at Debug level rather than Info.</summary>
    /// <remarks>10ms — keeps end-of-build noise from no-op hooks out of the default-level summary.</remarks>
    private const double SignificantSecondsThreshold = 0.010;

    /// <summary>Plugin-name → cumulative ticks. <see cref="ConcurrentDictionary{TKey,TValue}"/> for the parallel-render hook path; configure / finalize fire serially.</summary>
    private readonly ConcurrentDictionary<byte[], long> _ticks = new(ByteArrayComparer.Instance);

    /// <summary>Gets the threshold under which an entry drops to Debug in <see cref="Emit"/>.</summary>
    public static double SignificantSeconds => SignificantSecondsThreshold;

    /// <summary>Begins a measurement scope for <paramref name="pluginName"/>; disposing the returned scope adds the elapsed ticks to the running total.</summary>
    /// <param name="pluginName">Plugin <see cref="NuStreamDocs.Plugins.IDocPlugin.Name"/> bytes.</param>
    /// <returns>A scope to wrap with <c>using</c>.</returns>
    public MeasurementScope Measure(ReadOnlySpan<byte> pluginName)
    {
        if (pluginName.Length is 0)
        {
            throw new ArgumentException("Plugin name must be non-empty.", nameof(pluginName));
        }

        return new(this, [.. pluginName]);
    }

    /// <summary>Adds a pre-captured tick delta to <paramref name="pluginName"/>'s running total.</summary>
    /// <param name="pluginName">Plugin <see cref="NuStreamDocs.Plugins.IDocPlugin.Name"/> bytes.</param>
    /// <param name="elapsedTicks"><see cref="Stopwatch"/>-frequency tick delta from a caller-managed timestamp.</param>
    /// <remarks>Exposed for hot paths that already have the delta in hand (e.g. callers timing several invocations in one shot); prefer <see cref="Measure"/> elsewhere.</remarks>
    public void Add(byte[] pluginName, long elapsedTicks)
    {
        ArgumentNullException.ThrowIfNull(pluginName);
        if (pluginName.Length is 0)
        {
            throw new ArgumentException("Plugin name must be non-empty.", nameof(pluginName));
        }

        _ticks.AddOrUpdate(pluginName, static (_, ticks) => ticks, static (_, prev, ticks) => prev + ticks, elapsedTicks);
    }

    /// <summary>Writes the per-plugin total time to <paramref name="logger"/> sorted descending; sub-significant entries drop to Debug.</summary>
    /// <param name="logger">Target logger.</param>
    public void Emit(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var rows = Snapshot();
        if (rows.Length is 0)
        {
            return;
        }

        PluginTimingLoggingHelper.LogPluginTimingHeader(logger);
        for (var i = 0; i < rows.Length; i++)
        {
            var (name, seconds) = rows[i];
            if (seconds < SignificantSeconds)
            {
                PluginTimingLoggingHelper.LogPluginTimingDebug(logger, name, seconds);
            }
            else
            {
                PluginTimingLoggingHelper.LogPluginTimingRow(logger, name, seconds);
            }
        }
    }

    /// <summary>Returns a snapshot sorted by total time descending; primarily for tests.</summary>
    /// <returns>Per-plugin (UTF-8 name bytes, total seconds) entries.</returns>
    internal (byte[] Name, double Seconds)[] Snapshot()
    {
        var pairs = _ticks.ToArray();
        var result = new (byte[], double)[pairs.Length];
        for (var i = 0; i < pairs.Length; i++)
        {
            result[i] = (pairs[i].Key, Stopwatch.GetElapsedTime(0, pairs[i].Value).TotalSeconds);
        }

        Array.Sort(result, static (a, b) => b.Item2.CompareTo(a.Item2));
        return result;
    }

    /// <summary>Disposable scope returned from <see cref="Measure"/>; captures <see cref="Stopwatch.GetTimestamp"/> at entry and accumulates the delta on disposal.</summary>
    /// <remarks>The <c>using</c> compiler lowering disposes exactly once, so no re-entry guard is needed.</remarks>
    public readonly record struct MeasurementScope(PluginTimingTable Table, byte[] PluginName) : IDisposable
    {
        /// <summary>Stopwatch timestamp captured at scope entry.</summary>
        private readonly long _start = Stopwatch.GetTimestamp();

        /// <inheritdoc/>
        public void Dispose() => Table.Add(PluginName, Stopwatch.GetTimestamp() - _start);

        /// <inheritdoc/>
        public bool Equals(MeasurementScope other) =>
            _start == other._start &&
            ReferenceEquals(Table, other.Table) &&
            ByteArrayComparer.Instance.Equals(PluginName, other.PluginName);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(_start, Table, ByteArrayComparer.Instance.GetHashCode(PluginName));
    }
}
