// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Tests;

/// <summary>Behavior tests for <c>PhaseTimer</c>.</summary>
public class PhaseTimerTests
{
    /// <summary>The async overload runs the work and emits the start log before, the complete log after, in that order.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunAsyncEmitsStartThenCompleteAroundTheWork()
    {
        List<string> sequence = [];

        await PhaseTimer.RunAsync(
            NullLogger.Instance,
            _ => sequence.Add("start"),
            (_, _) => sequence.Add("complete"),
            () =>
            {
                sequence.Add("work");
                return ValueTask.CompletedTask;
            });

        await Assert.That(string.Join(',', sequence)).IsEqualTo("start,work,complete");
    }

    /// <summary>The complete log fires from a <c>finally</c> block when the work throws — the exception still propagates and the elapsed time is recorded.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunAsyncCompleteLogFiresEvenWhenWorkThrows()
    {
        var startCalls = 0;
        var completeCalls = 0;
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsTask(PhaseTimer.RunAsync(
                NullLogger.Instance,
                _ => startCalls++,
                (_, _) => completeCalls++,
                () => throw new InvalidOperationException("work failed"))));

        await Assert.That(exception).IsNotNull();
        await Assert.That(startCalls).IsEqualTo(1);
        await Assert.That(completeCalls).IsEqualTo(1);
    }

    /// <summary>The elapsed-seconds value handed to the complete delegate is non-negative and at least the duration of the await.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunAsyncMeasuresElapsedSecondsAroundTheAwait()
    {
        var captured = -1d;
        await PhaseTimer.RunAsync(
            NullLogger.Instance,
            static _ => { },
            (_, secs) => captured = secs,
            async () => await Task.Delay(50).ConfigureAwait(false));

        await Assert.That(captured).IsGreaterThanOrEqualTo(0d);
        await Assert.That(captured).IsGreaterThanOrEqualTo(0.04d); // 50ms ± clock granularity
    }

    /// <summary>The result-returning overload returns the value the work produced, hands the result to the completion delegate, and still emits both log entries.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunAsyncReturnsTheResultOfTheWork()
    {
        var startCalls = 0;
        var completeCalls = 0;
        var capturedResult = -1;
        var result = await PhaseTimer.RunAsync(
            NullLogger.Instance,
            _ => startCalls++,
            (_, value, _) =>
            {
                capturedResult = value;
                completeCalls++;
            },
            () => ValueTask.FromResult(42));

        await Assert.That(result).IsEqualTo(42);
        await Assert.That(capturedResult).IsEqualTo(42);
        await Assert.That(startCalls).IsEqualTo(1);
        await Assert.That(completeCalls).IsEqualTo(1);
    }

    /// <summary>The result-returning overload skips the completion log when the work throws — the start log is the only marker and the exception propagates.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunAsyncResultOverloadSkipsCompleteLogOnException()
    {
        var startCalls = 0;
        var completeCalls = 0;
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAsTask(PhaseTimer.RunAsync<int>(
                NullLogger.Instance,
                _ => startCalls++,
                (_, _, _) => completeCalls++,
                () => throw new InvalidOperationException("boom"))));

        await Assert.That(thrown).IsNotNull();
        await Assert.That(startCalls).IsEqualTo(1);
        await Assert.That(completeCalls).IsEqualTo(0);
    }

    /// <summary>The synchronous <c>Run</c> overload follows the same start / work / complete sequence.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunEmitsStartThenCompleteAroundSynchronousWork()
    {
        List<string> sequence = [];
        PhaseTimer.Run(
            NullLogger.Instance,
            _ => sequence.Add("start"),
            (_, _) => sequence.Add("complete"),
            () => sequence.Add("work"));

        await Assert.That(string.Join(',', sequence)).IsEqualTo("start,work,complete");
    }

    /// <summary>The synchronous <c>Run</c> overload still emits the complete log when the work throws.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunCompleteLogFiresEvenWhenSynchronousWorkThrows()
    {
        var completeCalls = 0;
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            PhaseTimer.Run(
                NullLogger.Instance,
                static _ => { },
                (_, _) => completeCalls++,
                () => throw new InvalidOperationException("sync work failed")));

        await Assert.That(thrown).IsNotNull();
        await Assert.That(completeCalls).IsEqualTo(1);
    }

    /// <summary>Null arguments to the async overload throw <see cref="ArgumentNullException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task RunAsyncRejectsNullArguments()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(static () =>
            RunAsTask(PhaseTimer.RunAsync(null!, static _ => { }, static (_, _) => { }, static () => ValueTask.CompletedTask)));
        await Assert.ThrowsAsync<ArgumentNullException>(static () =>
            RunAsTask(PhaseTimer.RunAsync(NullLogger.Instance, null!, static (_, _) => { }, static () => ValueTask.CompletedTask)));
        await Assert.ThrowsAsync<ArgumentNullException>(static () =>
            RunAsTask(PhaseTimer.RunAsync(NullLogger.Instance, static _ => { }, null!, static () => ValueTask.CompletedTask)));
        await Assert.ThrowsAsync<ArgumentNullException>(static () =>
            RunAsTask(PhaseTimer.RunAsync(NullLogger.Instance, static _ => { }, static (_, _) => { }, (Func<ValueTask>)null!)));
    }

    /// <summary>Adapts a single <see cref="ValueTask"/> instance into a <see cref="Task"/> so it's only consumed once even when fed to <c>Assert.ThrowsAsync</c>'s lambda.</summary>
    /// <param name="valueTask">Value task to adapt.</param>
    /// <returns>The equivalent <see cref="Task"/>.</returns>
    private static Task RunAsTask(ValueTask valueTask) => valueTask.AsTask();

    /// <summary>Adapts a single <see cref="ValueTask{T}"/> instance into a <see cref="Task{T}"/> for the same reason.</summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="valueTask">Value task to adapt.</param>
    /// <returns>The equivalent <see cref="Task{T}"/>.</returns>
    private static Task<T> RunAsTask<T>(ValueTask<T> valueTask) => valueTask.AsTask();
}
