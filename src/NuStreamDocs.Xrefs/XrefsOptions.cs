// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Xrefs;

/// <summary>Configuration for <see cref="XrefsPlugin"/>.</summary>
/// <param name="OutputFileName">Output filename for the emitted xrefmap (relative to the site root).</param>
/// <param name="BaseUrl">Optional base URL embedded in the emitted xrefmap. Consumers prepend this to every <c>href</c>.</param>
/// <param name="Imports">External xrefmaps to fetch and merge into the local registry at configure time.</param>
/// <param name="EmitMap">When false the plugin skips the emit pass; useful for sites that only consume external xrefmaps.</param>
public sealed record XrefsOptions(
    FilePath OutputFileName,
    byte[] BaseUrl,
    XrefImport[] Imports,
    bool EmitMap)
{
    /// <summary>Gets the default option set — emit <c>xrefmap.json</c>, no base URL, no imports.</summary>
    public static XrefsOptions Default { get; } = new("xrefmap.json", [], [], EmitMap: true);

    /// <summary>Throws when any required field is invalid.</summary>
    /// <exception cref="ArgumentException">When <see cref="OutputFileName"/> is null/empty/whitespace.</exception>
    /// <exception cref="ArgumentNullException">When <see cref="Imports"/> is null.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(OutputFileName);
        ArgumentNullException.ThrowIfNull(Imports);
    }
}
