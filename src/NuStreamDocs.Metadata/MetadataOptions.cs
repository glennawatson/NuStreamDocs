// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Metadata;

/// <summary>Configuration for <see cref="MetadataPlugin"/>.</summary>
/// <param name="DirectoryFileName">Directory-level metadata filename, applied to every page below the directory.</param>
/// <param name="SidecarSuffix">Per-page sidecar suffix appended to the page filename (e.g. <c>.meta.yml</c> turns <c>intro.md</c> into <c>intro.md.meta.yml</c>).</param>
public sealed record MetadataOptions(string DirectoryFileName, string SidecarSuffix)
{
    /// <summary>Gets the default option set — <c>_meta.yml</c> for directories, <c>.meta.yml</c> sidecars.</summary>
    public static MetadataOptions Default { get; } = new("_meta.yml", ".meta.yml");

    /// <summary>Throws when any field is empty or whitespace.</summary>
    /// <exception cref="ArgumentException">When a required field is null, empty, or whitespace.</exception>
    public void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DirectoryFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(SidecarSuffix);
    }
}
