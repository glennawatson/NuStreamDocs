// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>The result of rendering one fragment: either HTML or an error message.</summary>
/// <param name="Html">The rendered HTML, or <see langword="null"/> when rendering failed.</param>
/// <param name="Error">The failure description, or <see langword="null"/> when rendering succeeded.</param>
[DebuggerDisplay("Html={Html != null}, Error={Error}")]
internal sealed record RenderResult(string? Html, string? Error);
