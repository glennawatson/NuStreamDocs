// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>PluginTimingTable</c>.</summary>
public class PluginTimingTableTests
{
    /// <summary>An empty table snapshot returns an empty array.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmptyTableSnapshotReturnsEmpty()
    {
        PluginTimingTable table = new();
        var rows = SnapshotViaReflection(table);
        await Assert.That(rows.Length).IsEqualTo(0);
    }

    /// <summary>The <c>Measure</c> scope accumulates elapsed time into the named bucket.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MeasureScopeAccumulatesElapsed()
    {
        PluginTimingTable table = new();
        using (table.Measure([.. "plugin-a"u8]))
        {
            await Task.Delay(20);
        }

        var rows = SnapshotViaReflection(table);
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Name.SequenceEqual("plugin-a"u8)).IsTrue();
        await Assert.That(rows[0].Seconds).IsGreaterThan(0.010);
    }

    /// <summary>Multiple <c>Measure</c> scopes for the same plugin add to the same bucket.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepeatedMeasureScopesAccumulate()
    {
        PluginTimingTable table = new();
        for (var i = 0; i < 3; i++)
        {
            using (table.Measure([.. "plugin-a"u8]))
            {
                await Task.Delay(10);
            }
        }

        var rows = SnapshotViaReflection(table);
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Seconds).IsGreaterThan(0.025);
    }

    /// <summary>Snapshot rows are sorted by total time descending.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SnapshotIsSortedDescending()
    {
        PluginTimingTable table = new();
        table.Add([.. "fast"u8], 1_000);
        table.Add([.. "slow"u8], 100_000_000);
        table.Add([.. "medium"u8], 1_000_000);

        var rows = SnapshotViaReflection(table);
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
        table.Add([.. "plugin-a"u8], System.Diagnostics.Stopwatch.Frequency); // ~1s
        table.Add([.. "plugin-b"u8], System.Diagnostics.Stopwatch.Frequency / 2); // ~0.5s

        RecordingLogger logger = new();
        table.Emit(logger);

        await Assert.That(logger.Records.Count).IsEqualTo(3);
        await Assert.That(logger.Records[0].Message).Contains("Plugin timing summary");
        await Assert.That(logger.Records[1].Message).Contains("plugin-a");
        await Assert.That(logger.Records[2].Message).Contains("plugin-b");
    }

    /// <summary>Sub-significant entries log at Debug rather than Info.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SubSignificantEntriesUseDebugLevel()
    {
        PluginTimingTable table = new();
        table.Add([.. "plugin-a"u8], System.Diagnostics.Stopwatch.Frequency); // ~1s
        table.Add([.. "plugin-fast"u8], 1); // ~0s

        RecordingLogger logger = new();
        table.Emit(logger);

        var (slowLevel, _) = logger.Records.First(r => r.Message.Contains("plugin-a", StringComparison.Ordinal));
        var (fastLevel, _) = logger.Records.First(r => r.Message.Contains("plugin-fast", StringComparison.Ordinal));
        await Assert.That(slowLevel).IsEqualTo(LogLevel.Information);
        await Assert.That(fastLevel).IsEqualTo(LogLevel.Debug);
    }

    /// <summary>Calls the internal <c>Snapshot()</c> via reflection so tests can assert ordering / accumulation without exposing it on the public API.</summary>
    /// <param name="table">Table under test.</param>
    /// <returns>The snapshot rows.</returns>
    private static (byte[] Name, double Seconds)[] SnapshotViaReflection(PluginTimingTable table) =>
        (table.GetType()
            .GetMethod("Snapshot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(table, null) as (byte[] Name, double Seconds)[])!;

    /// <summary>Records every log entry for assertion.</summary>
    private sealed class RecordingLogger : ILogger
    {
        /// <summary>Gets the captured records (level + formatted message).</summary>
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            Records.Add((logLevel, formatter(state, exception)));
        }
    }
}
