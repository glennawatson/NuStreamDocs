// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>A launchable Python interpreter that satisfies the reference packages.</summary>
/// <param name="FileName">Executable to start.</param>
/// <param name="LeadingArguments">Arguments that must precede the script or module arguments, such as <c>-3</c> for the Windows launcher.</param>
/// <param name="Display">Human-readable command line used in diagnostics.</param>
/// <param name="Version">Interpreter version reported by the executable.</param>
[DebuggerDisplay("{Display} {Version}")]
internal sealed record PythonInterpreter(string FileName, string[] LeadingArguments, string Display, Version Version);
