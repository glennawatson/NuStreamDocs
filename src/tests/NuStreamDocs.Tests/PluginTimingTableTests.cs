// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>PluginTimingTable</c>.</summary>
public class PluginTimingTableTests
{
    /// <summary>Measure Delay Milliseconds used by the test cases.</summary>
    private const int MeasureDelayMilliseconds = 20;

    /// <summary>Minimum Measured Seconds used by the test cases.</summary>
    private const double MinimumMeasuredSeconds = 0.010;

    /// <summary>Measurement Count used by the test cases.</summary>
    private const int MeasurementCount = 3;

    /// <summary>Repeated Delay Milliseconds used by the test cases.</summary>
    private const int RepeatedDelayMilliseconds = 10;

    /// <summary>Minimum Accumulated Seconds used by the test cases.</summary>
    private const double MinimumAccumulatedSeconds = 0.025;

    /// <summary>Fast Ticks used by the test cases.</summary>
    private const int FastTicks = 1_000;

    /// <summary>Slow Ticks used by the test cases.</summary>
    private const int SlowTicks = 100_000_000;

    /// <summary>Medium Ticks used by the test cases.</summary>
    private const int MediumTicks = 1_000_000;

    /// <summary>Half Second Divisor used by the test cases.</summary>
    private const int HalfSecondDivisor = 2;

    /// <summary>Expected Log Record Count used by the test cases.</summary>
    private const int ExpectedLogRecordCount = 3;

    /// <summary>Plugin name expected in log records.</summary>
    private const string PluginNameText = "plugin-a";

    /// <summary>Gets the plugin name used by the test cases.</summary>
    private static ReadOnlySpan<byte> PluginName => "plugin-a"u8;

    /// <summary>An empty table snapshot returns an empty array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyTableSnapshotReturnsEmpty()
    {
        PluginTimingTable table = new();
        var rows = table.Snapshot();
        await Assert.That(rows.Length).IsEqualTo(0);
    }

    /// <summary>The <c>Measure</c> scope accumulates elapsed time into the named bucket.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MeasureScopeAccumulatesElapsed()
    {
        PluginTimingTable table = new();
        using (table.Measure([.. PluginName]))
        {
            await Task.Delay(MeasureDelayMilliseconds);
        }

        var rows = table.Snapshot();
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Name.SequenceEqual(PluginName)).IsTrue();
        await Assert.That(rows[0].Seconds).IsGreaterThan(MinimumMeasuredSeconds);
    }

    /// <summary>Multiple <c>Measure</c> scopes for the same plugin add to the same bucket.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepeatedMeasureScopesAccumulate()
    {
        PluginTimingTable table = new();
        for (var i = 0; i < MeasurementCount; i++)
        {
            using (table.Measure([.. PluginName]))
            {
                await Task.Delay(RepeatedDelayMilliseconds);
            }
        }

        var rows = table.Snapshot();
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Seconds).IsGreaterThan(MinimumAccumulatedSeconds);
    }

    /// <summary><c>MeasureStable</c> scopes accumulate into the same bucket as <c>Measure</c> scopes for the same plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MeasureStableAccumulatesIntoNamedBucket()
    {
        PluginTimingTable table = new();
        byte[] name = [.. PluginName];
        using (table.MeasureStable(name))
        {
            await Task.Delay(RepeatedDelayMilliseconds);
        }

        using (table.Measure(PluginName))
        {
            await Task.Delay(RepeatedDelayMilliseconds);
        }

        var rows = table.Snapshot();
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Name.SequenceEqual(PluginName)).IsTrue();
        await Assert.That(rows[0].Seconds).IsGreaterThan(MinimumMeasuredSeconds);
    }

    /// <summary><c>MeasureStable</c> rejects an empty plugin name.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MeasureStableRejectsEmptyName()
    {
        PluginTimingTable table = new();
        var ex = Assert.Throws<ArgumentException>(() => table.MeasureStable([]));
        await Assert.That(ex).IsNotNull();
    }

    /// <summary>Snapshot rows are sorted by total time descending.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SnapshotIsSortedDescending()
    {
        PluginTimingTable table = new();
        table.Add([.. "fast"u8], FastTicks);
        table.Add([.. "slow"u8], SlowTicks);
        table.Add([.. "medium"u8], MediumTicks);

        var rows = table.Snapshot();
        await Assert.That(rows[0].Name.SequenceEqual("slow"u8)).IsTrue();
        await Assert.That(rows[1].Name.SequenceEqual("medium"u8)).IsTrue();
        await Assert.That(rows[2].Name.SequenceEqual("fast"u8)).IsTrue();
    }

    /// <summary><c>Add</c> rejects null / empty plugin names.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddRejectsEmptyName()
    {
        PluginTimingTable table = new();
        var ex1 = Assert.Throws<ArgumentException>(() => table.Add([], 1));
        var ex2 = Assert.Throws<ArgumentException>(() => table.Measure([]));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
    }

    /// <summary><c>Emit</c> on an empty table is a no-op (no logger calls).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmitOnEmptyTableDoesNothing()
    {
        PluginTimingTable table = new();
        RecordingLogger logger = new();
        table.Emit(logger);
        await Assert.That(logger.Records.Count).IsEqualTo(0);
    }

    /// <summary><c>Emit</c> writes a header plus one row per plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmitWritesHeaderAndOneRowPerPlugin()
    {
        PluginTimingTable table = new();
        table.Add([.. PluginName], Stopwatch.Frequency); // ~1s
        table.Add([.. "plugin-b"u8], Stopwatch.Frequency / HalfSecondDivisor); // ~0.5s

        RecordingLogger logger = new();
        table.Emit(logger);

        await Assert.That(logger.Records.Count).IsEqualTo(ExpectedLogRecordCount);
        await Assert.That(logger.Records[0].Message).Contains("Plugin timing summary");
        await Assert.That(logger.Records[1].Message).Contains(PluginNameText);
        await Assert.That(logger.Records[2].Message).Contains("plugin-b");
    }

    /// <summary>Sub-significant entries log at Debug rather than Info.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubSignificantEntriesUseDebugLevel()
    {
        PluginTimingTable table = new();
        table.Add([.. PluginName], Stopwatch.Frequency); // ~1s
        table.Add([.. "plugin-fast"u8], 1); // ~0s

        RecordingLogger logger = new();
        table.Emit(logger);

        var slowRecord = logger.Records.FindIndex(static r => r.Message.Contains(PluginNameText, StringComparison.Ordinal));
        var fastRecord = logger.Records.FindIndex(static r => r.Message.Contains("plugin-fast", StringComparison.Ordinal));
        await Assert.That(slowRecord).IsGreaterThanOrEqualTo(0);
        await Assert.That(fastRecord).IsGreaterThanOrEqualTo(0);
        var (slowLevel, _) = logger.Records[slowRecord];
        var (fastLevel, _) = logger.Records[fastRecord];
        await Assert.That(slowLevel).IsEqualTo(LogLevel.Information);
        await Assert.That(fastLevel).IsEqualTo(LogLevel.Debug);
    }

    /// <summary>Records every log entry for assertion.</summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <summary>Gets the captured records (level + formatted message).</summary>
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Records.Add((logLevel, formatter(state, exception)));
        }
    }
}
