// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Logging;

namespace NuStreamDocs.Tests;

/// <summary>Coverage for LogInvokerHelper overloads.</summary>
public class LogInvokerHelperTests
{
    /// <summary>One-arg overload runs the action when logging is enabled.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OneArgInvokes()
    {
        var ran = false;
        LogInvokerHelper.Invoke(new EnabledLogger(), LogLevel.Information, "x", (_, _) => ran = true);
        await Assert.That(ran).IsTrue();
    }

    /// <summary>One-arg overload skips the action when logging is disabled.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task OneArgSkipsWhenDisabled()
    {
        var ran = false;
        LogInvokerHelper.Invoke(NullLogger.Instance, LogLevel.Information, "x", (_, _) => ran = true);
        await Assert.That(ran).IsFalse();
    }

    /// <summary>Projection overload runs the projection only when logging is enabled.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ProjectionRunsWhenEnabled()
    {
        var calls = 0;
        LogInvokerHelper.Invoke<string, int, int, int>(
            new EnabledLogger(),
            LogLevel.Information,
            "k",
            1,
            2,
            ProjectEnabled,
            (_, _, _, p) => calls += p);

        await Assert.That(calls).IsEqualTo(21);
        return;

        int ProjectEnabled(int x)
        {
            calls++;
            return x * 10;
        }
    }

    /// <summary>Projection overload skips the projection when logging is disabled.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ProjectionSkippedWhenDisabled()
    {
        var calls = 0;
        LogInvokerHelper.Invoke<string, int, int, int>(
            NullLogger.Instance,
            LogLevel.Information,
            "k",
            1,
            2,
            ProjectDisabled,
            (_, _, _, _) => calls = 1);

        await Assert.That(calls).IsEqualTo(0);
        return;

        int ProjectDisabled(int x)
        {
            calls = 99;
            return x;
        }
    }

    /// <summary>Logger that always reports enabled.</summary>
    private sealed class EnabledLogger : ILogger
    {
        /// <inheritdoc/>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        /// <inheritdoc/>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc/>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // Test logger swallows entries; success is measured by side effects of the gated callback.
        }
    }
}
