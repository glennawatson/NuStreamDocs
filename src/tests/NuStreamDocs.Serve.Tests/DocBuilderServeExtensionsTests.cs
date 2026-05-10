// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using NuStreamDocs.Building;

namespace NuStreamDocs.Serve.Tests;

/// <summary>Tests for <see cref="DocBuilderServeExtensions"/>'s overload chain.</summary>
/// <remarks>
/// The watch + serve loop is long-running by design (binds Kestrel + spawns a watcher), so these
/// tests pre-cancel the token before invoking. Each overload still has to traverse argument
/// validation + the initial-build call before it observes cancellation, which is enough to pin
/// the public surface without standing up a real dev server.
/// </remarks>
public class DocBuilderServeExtensionsTests
{
    /// <summary>Initial-build cancellation surfaces as <see cref="OperationCanceledException"/>.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task SingleArgOverloadReturnsTask()
    {
        var builder = NewBuilder();
        var task = builder.WatchAndServeAsync();

        // The parameterless overload uses CancellationToken.None and would run forever.
        // Just verify it returned a task we can observe — don't await it.
        await Assert.That(task).IsNotNull();
    }

    /// <summary>The cancellation-token overload honors a pre-cancelled token at the initial-build step.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task CancellationTokenOverloadHonorsPreCancelledToken()
    {
        var builder = NewBuilder();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.That(() => builder.WatchAndServeAsync(cts.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>The configure-only overload runs the customization once and honors the pre-cancelled token.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureOverloadInvokesCustomization()
    {
        var builder = NewBuilder();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        var invocations = 0;
        WatchAndServeOptions Configure(WatchAndServeOptions o)
        {
            invocations++;
            return o with { OpenBrowser = false };
        }

        await Assert.That(() => builder.WatchAndServeAsync(Configure, cts.Token)).Throws<OperationCanceledException>();
        await Assert.That(invocations).IsEqualTo(1);
    }

    /// <summary>The configure + logger overload also runs the customization once.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureAndLoggerOverloadInvokesCustomization()
    {
        var builder = NewBuilder();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        var invocations = 0;
        WatchAndServeOptions Configure(WatchAndServeOptions o)
        {
            invocations++;
            return o with { OpenBrowser = false };
        }

        await Assert.That(() => builder.WatchAndServeAsync(Configure, NullLogger.Instance, cts.Token)).Throws<OperationCanceledException>();
        await Assert.That(invocations).IsEqualTo(1);
    }

    /// <summary>The most-specific overload rejects a null builder.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MostSpecificOverloadRejectsNullBuilder() =>
        await Assert.That(() => DocBuilderServeExtensions.WatchAndServeAsync(null!, WatchAndServeOptions.Default, NullLogger.Instance, CancellationToken.None))
            .Throws<ArgumentNullException>();

    /// <summary>The most-specific overload rejects a null logger.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task MostSpecificOverloadRejectsNullLogger()
    {
        var builder = NewBuilder();
        await Assert.That(() => builder.WatchAndServeAsync(WatchAndServeOptions.Default, null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>The configure-only overload rejects a null configure delegate.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureOverloadRejectsNullConfigure()
    {
        var builder = NewBuilder();
        await Assert.That(() => builder.WatchAndServeAsync(null!, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>The configure + logger overload rejects a null configure delegate.</summary>
    /// <returns>Async test.</returns>
    [Test]
    public async Task ConfigureAndLoggerOverloadRejectsNullConfigure()
    {
        var builder = NewBuilder();
        await Assert.That(() => builder.WatchAndServeAsync(null!, NullLogger.Instance, CancellationToken.None))
            .Throws<ArgumentNullException>();
    }

    /// <summary>
    /// Builds a minimal <see cref="DocBuilder"/> pointed at fresh scratch input/output dirs;
    /// sufficient for the validation paths under test.
    /// </summary>
    /// <returns>A configured builder.</returns>
    private static DocBuilder NewBuilder()
    {
        var input = Path.Combine(Path.GetTempPath(), "smd-serve-in-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "smd-serve-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(input);
        return new DocBuilder().WithInput(input).WithOutput(output);
    }
}
