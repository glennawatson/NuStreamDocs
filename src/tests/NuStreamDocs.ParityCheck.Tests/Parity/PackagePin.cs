// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>A Python package pinned to one exact version.</summary>
/// <param name="Name">Normalized package name: lower case with dashes.</param>
/// <param name="Version">Exact version.</param>
[DebuggerDisplay("{Name}=={Version}")]
internal sealed record PackagePin(string Name, string Version);
