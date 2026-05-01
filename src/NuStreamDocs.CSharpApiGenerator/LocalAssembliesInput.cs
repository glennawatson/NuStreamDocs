// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Walks a caller-supplied set of pre-built <c>.dll</c> files. No NuGet
/// fetch happens; SourceLink lookups still run against any embedded
/// metadata in the assemblies themselves.
/// </summary>
/// <param name="Tfm">TFM the assemblies were built for (e.g. <c>net10.0</c>); used to stamp <c>ApiType.AppliesTo</c>.</param>
/// <param name="AssemblyPaths">Absolute paths to the <c>.dll</c> files to walk.</param>
/// <param name="FallbackSearchPaths">Additional directories whose contents the resolver consults when a transitive reference can't be located. Empty by default.</param>
public sealed record LocalAssembliesInput(
    string Tfm,
    string[] AssemblyPaths,
    string[] FallbackSearchPaths) : CSharpApiGeneratorInput
{
    /// <summary>Initializes a new instance of the <see cref="LocalAssembliesInput"/> class with no fallback search paths.</summary>
    /// <param name="tfm">TFM the assemblies were built for.</param>
    /// <param name="assemblyPaths">Absolute paths to the <c>.dll</c> files to walk.</param>
    public LocalAssembliesInput(string tfm, string[] assemblyPaths)
        : this(tfm, assemblyPaths, [])
    {
    }
}
