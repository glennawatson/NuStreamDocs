// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace NuStreamDocs.ParityCheck.Tests;

/// <summary>A corpus fragment whose sidecar kind has a generated pinned-output row.</summary>
/// <param name="Id">Fragment id.</param>
[DebuggerDisplay("{Id}")]
public sealed record SidecarCase(string Id)
{
    /// <inheritdoc/>
    public override string ToString() => Id;
}
