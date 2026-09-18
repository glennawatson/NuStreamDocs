// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Runs a Pagefind command on the host operating system.</summary>
internal sealed class PagefindProcess : IPagefindProcess
{
    /// <summary>Process owned by this invocation.</summary>
    private readonly Process _process = new();

    /// <inheritdoc/>
    public Stream StandardOutput => _process.StandardOutput.BaseStream;

    /// <inheritdoc/>
    public Stream StandardError => _process.StandardError.BaseStream;

    /// <inheritdoc/>
    public void Start(ProcessStartInfo startInfo)
    {
        _process.StartInfo = startInfo;
        _ = _process.Start();
    }

    /// <inheritdoc/>
    public async ValueTask<(int ExitCode, PosixSignal? Signal)> WaitForExitAsync(CancellationToken cancellationToken)
    {
#if NET11_0_OR_GREATER
        var status = await _process.WaitForExitStatusAsync(cancellationToken).ConfigureAwait(false);
        return (status.ExitCode, status.Signal);
#else
        await _process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (_process.ExitCode, null);
#endif
    }

    /// <inheritdoc/>
    public void KillIfRunning()
    {
        try
        {
            _process.Kill(true);
        }
        catch (InvalidOperationException) when (_process.HasExited)
        {
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _process.Dispose();
}
