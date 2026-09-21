// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>An attribute read from an HTML tag.</summary>
/// <param name="Name">Lower-case attribute name.</param>
/// <param name="Value">Canonical attribute value, or <see langword="null"/> for a bare attribute.</param>
[DebuggerDisplay("{Name}={Value}")]
internal sealed record HtmlAttribute(string Name, string? Value);
