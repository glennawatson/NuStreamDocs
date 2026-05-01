// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.SphinxInventory;

/// <summary>
/// Options for <see cref="SphinxInventoryPlugin"/>.
/// </summary>
/// <param name="ProjectName">Project name written to the inventory header (Sphinx <c>project</c>).</param>
/// <param name="Version">Project version written to the inventory header (Sphinx <c>version</c>); empty when no version is set.</param>
/// <param name="OutputFileName">File name written under the build output root; defaults to <c>objects.inv</c>.</param>
public sealed record SphinxInventoryOptions(
    string ProjectName,
    string Version,
    string OutputFileName)
{
    /// <summary>Gets the default option set — project <c>NuStreamDocs</c>, no version, written as <c>objects.inv</c>.</summary>
    public static SphinxInventoryOptions Default { get; } = new(
        ProjectName: "NuStreamDocs",
        Version: string.Empty,
        OutputFileName: "objects.inv");
}
