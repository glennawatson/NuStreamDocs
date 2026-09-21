// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The outcome of running an external process to completion.</summary>
/// <param name="Started">Whether the operating system started the process.</param>
/// <param name="ExitCode">Exit code, or -1 when the process did not start or timed out.</param>
/// <param name="StandardOutput">Everything the process wrote to standard output.</param>
/// <param name="StandardError">Everything the process wrote to standard error, or the reason it did not start.</param>
[DebuggerDisplay("Started={Started}, ExitCode={ExitCode}")]
internal sealed record ProcessResult(bool Started, int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>Gets a value indicating whether the process started and exited with code zero.</summary>
    public bool Succeeded => Started && ExitCode is 0;

    /// <summary>Gets standard output and standard error joined for diagnostics.</summary>
    public string CombinedOutput => (StandardOutput + Environment.NewLine + StandardError).Trim();
}
