// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>Verifies Pagefind invocation behavior with controlled process responses.</summary>
public sealed class PagefindCliTests
{
    /// <summary>Ordinary nonzero exit code distinct from a termination signal.</summary>
    private const int FailureExitCode = 143;

    /// <summary>Pinned-version metadata is available without starting a process.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PinnedVersionIsSet()
    {
        await Assert.That(PagefindCli.PinnedVersion).IsNotNull();
        await Assert.That(PagefindCli.PinnedVersion).IsNotEmpty();
    }

    /// <summary>Disabling indexing avoids creating a process.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunCliDisabledShortCircuits(CancellationToken cancellationToken)
    {
        var result = await PagefindCli.RunAsync(
            "/site",
            PagefindOptions.Default with { RunCli = false },
            NullLogger.Instance,
            static () => throw new InvalidOperationException("A disabled invocation must not create a process."),
            cancellationToken);

        await Assert.That(result).IsFalse();
    }

    /// <summary>An empty site root is rejected before process creation.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunAsync_EmptyRoot_Throws(CancellationToken cancellationToken) =>
        await Assert.That(() => PagefindCli.RunAsync(
            default,
            PagefindOptions.Default,
            NullLogger.Instance,
            static () => throw new InvalidOperationException("Invalid input must not create a process."),
            cancellationToken)).Throws<ArgumentException>();

    /// <summary>Normal exit codes determine success without being interpreted as signals.</summary>
    /// <param name="exitCode">Ordinary exit code reported by the process.</param>
    /// <param name="expected">Expected success result.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(0, true)]
    [Arguments(23, false)]
    [Arguments(FailureExitCode, false)]
    public async Task RunAsync_NormalExit_ReturnsSuccess(int exitCode, bool expected, CancellationToken cancellationToken)
    {
        using var files = new PagefindTestFiles();
        using var process = new StubPagefindProcess { ExitStatus = (exitCode, null) };

        var result = await InvokeAsync(files, process, false, cancellationToken);

        await Assert.That(result).IsEqualTo(expected);
        await Assert.That(process.WasDisposed).IsTrue();
    }

    /// <summary>Strict failures preserve the ordinary exit code and captured stderr.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunAsync_StrictNormalExit_ReportsExitCode(CancellationToken cancellationToken)
    {
        using var files = new PagefindTestFiles();
        using var process = new StubPagefindProcess { ExitStatus = (FailureExitCode, null), StandardError = new MemoryStream([.. "indexing-failed"u8]) };

        var exception = await Assert.That(() => InvokeAsync(files, process, true, cancellationToken)).Throws<InvalidOperationException>();

        await Assert.That(exception!.Message).Contains("Pagefind exited with code 143");
        await Assert.That(exception.Message).Contains("indexing-failed");
        await Assert.That(process.WasDisposed).IsTrue();
    }

    /// <summary>Signal termination is reported independently of ordinary exit codes.</summary>
    /// <param name="strict">Whether failure throws instead of returning false.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_SignalTermination_ReportsFailure(bool strict, CancellationToken cancellationToken)
    {
        using var files = new PagefindTestFiles();
        using var process = new StubPagefindProcess { ExitStatus = (0, PosixSignal.SIGTERM), StandardError = new MemoryStream([.. "interrupted"u8]) };

        if (!strict)
        {
            await Assert.That(await InvokeAsync(files, process, false, cancellationToken)).IsFalse();
            return;
        }

        var exception = await Assert.That(() => InvokeAsync(files, process, true, cancellationToken)).Throws<InvalidOperationException>();
        await Assert.That(exception!.Message).Contains("Pagefind terminated by signal SIGTERM");
        await Assert.That(exception.Message).Contains("interrupted");
    }

    /// <summary>Startup failures respect strict mode and dispose the process.</summary>
    /// <param name="strict">Whether startup failures propagate.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task RunAsync_StartFails_RespectsStrictMode(bool strict, CancellationToken cancellationToken)
    {
        using var files = new PagefindTestFiles();
        var failure = new InvalidOperationException("start-failed");
        using var process = new StubPagefindProcess { StartException = failure };

        if (strict)
        {
            var exception = await Assert.That(() => InvokeAsync(files, process, true, cancellationToken)).Throws<InvalidOperationException>();
            await Assert.That(ReferenceEquals(exception, failure)).IsTrue();
        }
        else
        {
            await Assert.That(await InvokeAsync(files, process, false, cancellationToken)).IsFalse();
        }

        await Assert.That(process.WasDisposed).IsTrue();
    }

    /// <summary>The command receives the site paths and consumes output beyond one drain buffer.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task RunAsync_ConfiguresCommandAndDrainsOutput(CancellationToken cancellationToken)
    {
        const int OutputLength = 10_000;
        using var files = new PagefindTestFiles();
        using var process = new StubPagefindProcess { StandardOutput = new MemoryStream(new byte[OutputLength]) };

        await Assert.That(await InvokeAsync(files, process, true, cancellationToken)).IsTrue();
        var startInfo = process.StartInfo!;
        await Assert.That(startInfo.FileName).IsEqualTo(files.Binary.Value);
        await Assert.That(startInfo.WorkingDirectory).IsEqualTo(files.Root);
        await Assert.That(startInfo.UseShellExecute).IsFalse();
        await Assert.That(startInfo.RedirectStandardOutput).IsTrue();
        await Assert.That(startInfo.RedirectStandardError).IsTrue();
        await Assert.That(startInfo.ArgumentList.SequenceEqual(["--site", files.Root, "--output-subdir", "pagefind", "--quiet"])).IsTrue();
        await Assert.That(process.StdoutBytesRead).IsEqualTo(OutputLength);
    }

    /// <summary>Cancellation after output closes terminates the pending process and propagates to the caller.</summary>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Timeout(10_000)]
    public async Task RunAsync_CanceledAfterOutputCloses_KillsChild(CancellationToken cancellationToken)
    {
        using var files = new PagefindTestFiles();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var process = new StubPagefindProcess { WaitForCancellation = true, OnWait = cancellation.Cancel };

        await Assert.That(() => InvokeAsync(files, process, true, cancellation.Token)).Throws<OperationCanceledException>();
        await Assert.That(process.KillCalls).IsEqualTo(1);
        await Assert.That(process.WasDisposed).IsTrue();
    }

    /// <summary>Invokes the runner with a controlled process and an existing binary-path placeholder.</summary>
    /// <param name="files">Isolated source paths.</param>
    /// <param name="process">Controlled process response.</param>
    /// <param name="strict">Whether failures propagate.</param>
    /// <param name="cancellationToken">Test cancellation token.</param>
    /// <returns>The runner's success result.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Task<bool> InvokeAsync(PagefindTestFiles files, StubPagefindProcess process, bool strict, CancellationToken cancellationToken) =>
        PagefindCli.RunAsync(
            files.Root,
            PagefindOptions.Default with { BinaryPath = files.Binary, StrictBinaryRequired = strict },
            NullLogger.Instance,
            () => process,
            cancellationToken);
}
