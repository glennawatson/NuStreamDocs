// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Fetches a caller-supplied list of NuGet packages directly — no
/// on-disk manifest required. The plugin synthesizes a transient
/// manifest in a scratch subdirectory of <paramref name="ApiCachePath"/>
/// before handing off to <c>NuGetAssemblySource</c>.
/// </summary>
/// <param name="Packages">Packages to fetch.</param>
/// <param name="ApiCachePath">Destination root for fetched packages and the synthesized manifest.</param>
/// <param name="TfmPreference">Ordered list of preferred TFMs. Empty falls back to <see cref="DefaultTfmPreference"/>.</param>
public sealed record NuGetPackagesInput(
    NuGetPackageReference[] Packages,
    DirectoryPath ApiCachePath,
    string[] TfmPreference) : CSharpApiGeneratorInput
{
    /// <summary>Initializes a new instance of the <see cref="NuGetPackagesInput"/> class with <see cref="DefaultTfmPreference"/>.</summary>
    /// <param name="packages">Packages to fetch.</param>
    /// <param name="apiCachePath">Destination root for fetched packages.</param>
    public NuGetPackagesInput(NuGetPackageReference[] packages, DirectoryPath apiCachePath)
        : this(packages, apiCachePath, DefaultTfmPreference)
    {
    }

    /// <summary>Gets the default TFM preference list — newest .NET first, then .NET Standard 2.0 as the broad fallback.</summary>
    public static string[] DefaultTfmPreference { get; } =
        ["net10.0", "net9.0", "net8.0", "netstandard2.1", "netstandard2.0"];
}
