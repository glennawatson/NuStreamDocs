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
        var table = new PluginTimingTable();
        var rows = table.GetType()
            .GetMethod("Snapshot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(table, null) as (string Name, double Seconds)[];
        await Assert.That(rows!.Length).IsEqualTo(0);
    }

    /// <summary>The <c>Measure</c> scope accumulates elapsed time into the named bucket.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MeasureScopeAccumulatesElapsed()
    {
        var table = new PluginTimingTable();
        using (table.Measure("plugin-a"))
        {
            await Task.Delay(20);
        }

        var rows = SnapshotViaReflection(table);
        await Assert.That(rows.Length).IsEqualTo(1);
        await Assert.That(rows[0].Name).IsEqualTo("plugin-a");
        await Assert.That(rows[0].Seconds).IsGreaterThan(0.010);
    }

    /// <summary>Multiple <c>Measure</c> scopes for the same plugin add to the same bucket.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RepeatedMeasureScopesAccumulate()
    {
        var table = new PluginTimingTable();
        for (var i = 0; i < 3; i++)
        {
            using (table.Measure("plugin-a"))
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
        var table = new PluginTimingTable();
        table.Add("fast", 1_000);
        table.Add("slow", 100_000_000);
        table.Add("medium", 1_000_000);

        var rows = SnapshotViaReflection(table);
        await Assert.That(rows[0].Name).IsEqualTo("slow");
        await Assert.That(rows[1].Name).IsEqualTo("medium");
        await Assert.That(rows[2].Name).IsEqualTo("fast");
    }

    /// <summary><c>Add</c> rejects null / empty plugin names.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task AddRejectsEmptyName()
    {
        var table = new PluginTimingTable();
        var ex1 = Assert.Throws<ArgumentException>(() => table.Add(string.Empty, 1));
        var ex2 = Assert.Throws<ArgumentException>(() => table.Measure(string.Empty));
        await Assert.That(ex1).IsNotNull();
        await Assert.That(ex2).IsNotNull();
    }

    /// <summary><c>Emit</c> on an empty table is a no-op (no logger calls).</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmitOnEmptyTableDoesNothing()
    {
        var table = new PluginTimingTable();
        var logger = new RecordingLogger();
        table.Emit(logger);
        await Assert.That(logger.Records.Count).IsEqualTo(0);
    }

    /// <summary><c>Emit</c> writes a header plus one row per plugin.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task EmitWritesHeaderAndOneRowPerPlugin()
    {
        var table = new PluginTimingTable();
        table.Add("plugin-a", System.Diagnostics.Stopwatch.Frequency); // ~1s
        table.Add("plugin-b", System.Diagnostics.Stopwatch.Frequency / 2); // ~0.5s

        var logger = new RecordingLogger();
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
        var table = new PluginTimingTable();
        table.Add("plugin-a", System.Diagnostics.Stopwatch.Frequency); // ~1s
        table.Add("plugin-fast", 1); // ~0s

        var logger = new RecordingLogger();
        table.Emit(logger);

        var (slowLevel, _) = logger.Records.First(r => r.Message.Contains("plugin-a", StringComparison.Ordinal));
        var (fastLevel, _) = logger.Records.First(r => r.Message.Contains("plugin-fast", StringComparison.Ordinal));
        await Assert.That(slowLevel).IsEqualTo(LogLevel.Information);
        await Assert.That(fastLevel).IsEqualTo(LogLevel.Debug);
    }

    /// <summary>Calls the internal <c>Snapshot()</c> via reflection so tests can assert ordering / accumulation without exposing it on the public API.</summary>
    /// <param name="table">Table under test.</param>
    /// <returns>The snapshot rows.</returns>
    private static (string Name, double Seconds)[] SnapshotViaReflection(PluginTimingTable table) =>
        (table.GetType()
            .GetMethod("Snapshot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(table, null) as (string Name, double Seconds)[])!;

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
