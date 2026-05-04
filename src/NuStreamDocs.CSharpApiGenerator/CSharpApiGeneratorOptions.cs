// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;
using SourceDocParser;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Configuration options for the C# reference generator.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Inputs"/> is a list of input shapes — manifest, inline
/// packages, local DLLs, custom <see cref="IAssemblySource"/> — that
/// the generator concatenates into a single walk. A single entry runs
/// the underlying source directly; multiple entries are wrapped in a
/// composite, letting callers mix shapes (e.g. NuGet manifest for
/// third-party packages plus local DLLs for in-development assemblies).
/// </para>
/// <para>
/// <see cref="Mode"/> selects how the result is delivered: emit
/// Markdown into the docs tree (the default, picked up by normal page
/// discovery) or stash the catalog in memory for direct consumers.
/// </para>
/// <para>
/// Use the <c>From*</c> factory helpers in callers; constructing the
/// record by hand is supported but verbose.
/// </para>
/// </remarks>
/// <param name="Inputs">Assembly-acquisition strategies; concatenated in order.</param>
/// <param name="OutputMarkdownSubdirectory">Output subdirectory under the docs root for emit-mode Markdown.</param>
/// <param name="Mode">How metadata is handed to the build pipeline; defaults to <see cref="CSharpApiGeneratorMode.EmitMarkdown"/>.</param>
public sealed record CSharpApiGeneratorOptions(
    CSharpApiGeneratorInput[] Inputs,
    PathSegment OutputMarkdownSubdirectory,
    CSharpApiGeneratorMode Mode)
{
    /// <summary>Gets the default subdirectory name for emit-mode reference pages.</summary>
    public static PathSegment DefaultOutputSubdirectory => new("api");

    /// <summary>Builds an options record for one or more input shapes using <see cref="DefaultOutputSubdirectory"/> and <see cref="CSharpApiGeneratorMode.EmitMarkdown"/>.</summary>
    /// <param name="inputs">One or more input shapes.</param>
    /// <returns>An options record.</returns>
    public static CSharpApiGeneratorOptions From(params ReadOnlySpan<CSharpApiGeneratorInput> inputs) =>
        new([.. inputs], DefaultOutputSubdirectory, CSharpApiGeneratorMode.EmitMarkdown);

    /// <summary>Builds an options record for the manifest input shape.</summary>
    /// <param name="rootDirectory">Repository root containing <c>nuget-packages.json</c>.</param>
    /// <param name="apiCachePath">Destination root for fetched packages.</param>
    /// <returns>An options record using <see cref="DefaultOutputSubdirectory"/> and <see cref="CSharpApiGeneratorMode.EmitMarkdown"/>.</returns>
    public static CSharpApiGeneratorOptions FromManifest(DirectoryPath rootDirectory, DirectoryPath apiCachePath) =>
        From(new NuGetManifestInput(rootDirectory, apiCachePath));

    /// <summary>Builds an options record for the manifest input shape with a custom output subdirectory.</summary>
    /// <param name="rootDirectory">Repository root containing <c>nuget-packages.json</c>.</param>
    /// <param name="apiCachePath">Destination root for fetched packages.</param>
    /// <param name="outputMarkdownSubdirectory">Output subdirectory under the docs root.</param>
    /// <returns>An options record using <see cref="CSharpApiGeneratorMode.EmitMarkdown"/>.</returns>
    public static CSharpApiGeneratorOptions FromManifest(DirectoryPath rootDirectory, DirectoryPath apiCachePath, PathSegment outputMarkdownSubdirectory) =>
        new([new NuGetManifestInput(rootDirectory, apiCachePath)], outputMarkdownSubdirectory, CSharpApiGeneratorMode.EmitMarkdown);

    /// <summary>Builds an options record for the inline-NuGet-packages shape.</summary>
    /// <param name="packages">Packages to fetch.</param>
    /// <param name="apiCachePath">Destination root for fetched packages and the synthesized manifest.</param>
    /// <returns>An options record using <see cref="DefaultOutputSubdirectory"/> and <see cref="CSharpApiGeneratorMode.EmitMarkdown"/>.</returns>
    public static CSharpApiGeneratorOptions FromPackages(NuGetPackageReference[] packages, DirectoryPath apiCachePath) =>
        From(new NuGetPackagesInput(packages, apiCachePath));

    /// <summary>Builds an options record for local pre-built assemblies.</summary>
    /// <param name="tfm">TFM the assemblies were built for.</param>
    /// <param name="assemblyPaths">Absolute paths to the <c>.dll</c> files to walk.</param>
    /// <returns>An options record using <see cref="DefaultOutputSubdirectory"/> and <see cref="CSharpApiGeneratorMode.EmitMarkdown"/>.</returns>
    public static CSharpApiGeneratorOptions FromAssemblies(ApiCompatString tfm, FilePath[] assemblyPaths) =>
        From(new LocalAssembliesInput(tfm, assemblyPaths));

    /// <summary>Builds an options record around a caller-supplied <see cref="IAssemblySource"/>.</summary>
    /// <param name="source">The caller-built source.</param>
    /// <returns>An options record using <see cref="DefaultOutputSubdirectory"/> and <see cref="CSharpApiGeneratorMode.EmitMarkdown"/>.</returns>
    public static CSharpApiGeneratorOptions FromSource(IAssemblySource source) =>
        From(new CustomInput(source));

    /// <summary>Throws when the input shape's required fields are empty or whitespace.</summary>
    /// <exception cref="ArgumentException">When a required string field is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException">When <see cref="Inputs"/> or any of its required reference fields are null.</exception>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(Inputs);
        if (OutputMarkdownSubdirectory.IsEmpty)
        {
            throw new ArgumentException("OutputMarkdownSubdirectory must be non-empty.", nameof(OutputMarkdownSubdirectory));
        }

        if (Inputs.Length is 0)
        {
            throw new ArgumentException("At least one input must be supplied.", nameof(Inputs));
        }

        for (var i = 0; i < Inputs.Length; i++)
        {
            ValidateInput(Inputs[i]);
        }
    }

    /// <summary>Per-shape required-field validation.</summary>
    /// <param name="input">Input to validate.</param>
    private static void ValidateInput(CSharpApiGeneratorInput input)
    {
        switch (input)
        {
            case NuGetManifestInput m:
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(m.RootDirectory);
                    ArgumentException.ThrowIfNullOrWhiteSpace(m.ApiCachePath);
                    return;
                }

            case NuGetPackagesInput p:
                {
                    ArgumentNullException.ThrowIfNull(p.Packages);
                    ArgumentException.ThrowIfNullOrWhiteSpace(p.ApiCachePath);
                    ArgumentNullException.ThrowIfNull(p.TfmPreference);
                    return;
                }

            case LocalAssembliesInput l:
                {
                    ArgumentException.ThrowIfNullOrWhiteSpace(l.Tfm);
                    ArgumentNullException.ThrowIfNull(l.AssemblyPaths);
                    ArgumentNullException.ThrowIfNull(l.FallbackSearchPaths);
                    return;
                }

            case CustomInput c:
                {
                    ArgumentNullException.ThrowIfNull(c.Source);
                    return;
                }

            case null:
                throw new ArgumentNullException(nameof(input));
            default:
                throw new ArgumentException($"Unknown input shape: {input.GetType().FullName}", nameof(input));
        }
    }
}
