// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>A Markdown fragment with an optional sidecar describing why it does not simply equal the primary reference.</summary>
/// <param name="Id">Fragment id: the path below the corpus directory without extension, using forward slashes.</param>
/// <param name="Feature">First path segment of the id, or <c>root</c> for a fragment directly in the corpus directory.</param>
/// <param name="Markdown">Markdown source rendered exactly as stored.</param>
/// <param name="Expect">The sidecar, when the fragment has one.</param>
[DebuggerDisplay("{Id}")]
internal sealed record Fragment(string Id, string Feature, string Markdown, Expectation? Expect);
