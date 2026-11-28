// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace NuStreamDocs.GitInfo;

/// <summary>Target-framework compatibility helper for awaiting process completion.</summary>
internal static class ProcessCompat
{
    /// <summary>Awaits <paramref name="process"/> termination and returns <c>true</c> when its exit code is zero.</summary>
    /// <param name="process">Started process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when the process exited cleanly with code <c>0</c>.</returns>
    internal static async ValueTask<bool> WaitForZeroExitAsync(Process process, CancellationToken cancellationToken)
    {
#if NET11_0_OR_GREATER
        var status = await process.WaitForExitStatusAsync(cancellationToken).ConfigureAwait(false);
        return status.ExitCode is 0;
#else
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode is 0;
#endif
    }
}
