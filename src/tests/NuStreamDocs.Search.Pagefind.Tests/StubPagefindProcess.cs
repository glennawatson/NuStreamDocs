// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NuStreamDocs.Search.Pagefind.Tests;

/// <summary>Provides streams and controllable termination for process lifecycle tests.</summary>
internal sealed class StubPagefindProcess : IPagefindProcess
{
    /// <summary>Completion controlled by startup or termination.</summary>
    private readonly TaskCompletionSource _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public Stream StandardOutput { get; init; } = Stream.Null;

    /// <inheritdoc/>
    public Stream StandardError { get; init; } = Stream.Null;

    /// <summary>Gets the termination result supplied to the caller.</summary>
    internal (int ExitCode, PosixSignal? Signal) ExitStatus { get; init; }

    /// <summary>Gets the failure raised during startup, if configured.</summary>
    internal Exception? StartException { get; init; }

    /// <summary>Gets a value indicating whether termination waits for cancellation.</summary>
    internal bool WaitForCancellation { get; init; }

    /// <summary>Gets the action triggered when the caller starts waiting for termination.</summary>
    internal Action? OnWait { get; init; }

    /// <summary>Gets the command received at startup.</summary>
    internal ProcessStartInfo? StartInfo { get; private set; }

    /// <summary>Gets the number of termination requests.</summary>
    internal int KillCalls { get; private set; }

    /// <summary>Gets a value indicating whether the caller disposed the invocation.</summary>
    internal bool WasDisposed { get; private set; }

    /// <summary>Gets the standard output bytes consumed before disposal.</summary>
    internal long StdoutBytesRead { get; private set; }

    /// <inheritdoc/>
    public void Start(ProcessStartInfo startInfo)
    {
        StartInfo = startInfo;
        if (StartException is { } exception)
        {
            throw exception;
        }

        if (!WaitForCancellation)
        {
            _exit.SetResult();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int ExitCode, PosixSignal? Signal)> WaitForExitAsync(CancellationToken cancellationToken)
    {
        OnWait?.Invoke();
        await _exit.Task.WaitAsync(cancellationToken);
        return ExitStatus;
    }

    /// <inheritdoc/>
    public void KillIfRunning()
    {
        KillCalls++;
        _ = _exit.TrySetResult();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (WasDisposed)
        {
            return;
        }

        StdoutBytesRead = StandardOutput.CanSeek ? StandardOutput.Position : 0;
        StandardOutput.Dispose();
        StandardError.Dispose();
        WasDisposed = true;
    }
}
