// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NuStreamDocs.Search.Pagefind;

/// <summary>Controls a Pagefind invocation and its output streams.</summary>
internal interface IPagefindProcess : IDisposable
{
    /// <summary>Gets the process's standard output after startup.</summary>
    Stream StandardOutput { get; }

    /// <summary>Gets the process's standard error after startup.</summary>
    Stream StandardError { get; }

    /// <summary>Starts the configured command.</summary>
    /// <param name="startInfo">Executable, arguments, and stream settings.</param>
    void Start(ProcessStartInfo startInfo);

    /// <summary>Waits for termination and reports its cause.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The ordinary exit code and termination signal when available.</returns>
    ValueTask<(int ExitCode, PosixSignal? Signal)> WaitForExitAsync(CancellationToken cancellationToken);

    /// <summary>Terminates the process tree if it is running.</summary>
    void KillIfRunning();
}
