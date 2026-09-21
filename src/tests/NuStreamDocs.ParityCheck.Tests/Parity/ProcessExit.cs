// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>How a process ended.</summary>
/// <param name="ExitCode">The exit code.</param>
/// <param name="Note">Extra explanation such as a terminating signal, or empty.</param>
[DebuggerDisplay("ExitCode={ExitCode} {Note}")]
internal sealed record ProcessExit(int ExitCode, string Note);
