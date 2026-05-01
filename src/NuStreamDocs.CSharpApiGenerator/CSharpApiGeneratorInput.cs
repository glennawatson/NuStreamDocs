// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Discriminated input source for <see cref="CSharpApiGeneratorOptions"/>.
/// One of <see cref="NuGetManifestInput"/>, <see cref="NuGetPackagesInput"/>,
/// <see cref="LocalAssembliesInput"/>, or <see cref="CustomInput"/> — the
/// generator dispatches on the concrete shape at run time.
/// </summary>
public abstract record CSharpApiGeneratorInput
{
    /// <summary>Initializes a new instance of the <see cref="CSharpApiGeneratorInput"/> class. Internal-only constructor; sealed subtypes live in sibling files.</summary>
    private protected CSharpApiGeneratorInput()
    {
    }
}
