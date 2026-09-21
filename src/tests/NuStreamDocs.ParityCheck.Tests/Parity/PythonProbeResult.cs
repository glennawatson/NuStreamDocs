// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The outcome of searching the machine for a suitable Python interpreter.</summary>
/// <param name="Interpreter">The first interpreter that satisfies the minimum version, or <see langword="null"/> when none does.</param>
/// <param name="Attempts">One line per probed candidate describing what was found.</param>
[DebuggerDisplay("Found={Interpreter != null}, Attempts={Attempts.Length}")]
internal sealed record PythonProbeResult(PythonInterpreter? Interpreter, string[] Attempts)
{
    /// <summary>Describes why no interpreter was usable, naming every candidate that was probed.</summary>
    /// <returns>A one-line explanation suitable for a skip reason.</returns>
    internal string DescribeMissing() =>
        $"No Python {PythonProbe.MinimumVersion.Major}.{PythonProbe.MinimumVersion.Minor} or newer was found. Probed: {string.Join("; ", Attempts)}.";
}
