// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text.Json;
using SourceDocParser;
using SourceDocParser.NuGet.Infrastructure;

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Builds an <see cref="IAssemblySource"/> from one or more
/// <see cref="CSharpApiGeneratorInput"/> shapes. Centralizes the
/// per-shape dispatch so <see cref="CSharpApiGenerator"/>'s public
/// entry points stay shape-agnostic.
/// </summary>
internal static class AssemblySourceFactory
{
    /// <summary>Synthesized-manifest scratch directory name created under the supplied cache root.</summary>
    private const string SynthesizedManifestDirectory = ".csharp-apigen-manifest";

    /// <summary>Synthesized manifest filename matching the on-disk format <c>NuGetAssemblySource</c> reads.</summary>
    private const string ManifestFileName = "nuget-packages.json";

    /// <summary>
    /// Resolves <paramref name="inputs"/> into a single
    /// <see cref="IAssemblySource"/>. When the array contains exactly
    /// one entry the underlying source is returned directly; multiple
    /// entries are wrapped in a <see cref="CompositeAssemblySource"/>.
    /// </summary>
    /// <param name="inputs">Caller-supplied input shapes.</param>
    /// <param name="logger">Logger handed to NuGet-driven sources.</param>
    /// <returns>The resolved source.</returns>
    public static IAssemblySource Create(CSharpApiGeneratorInput[] inputs, ILogger logger)
    {
        if (inputs.Length is 1)
        {
            return CreateOne(inputs[0], logger);
        }

        var sources = new IAssemblySource[inputs.Length];
        for (var i = 0; i < inputs.Length; i++)
        {
            sources[i] = CreateOne(inputs[i], logger);
        }

        return new CompositeAssemblySource(sources);
    }

    /// <summary>Resolves a single <see cref="CSharpApiGeneratorInput"/>.</summary>
    /// <param name="input">Input shape.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>The resolved source.</returns>
    internal static IAssemblySource CreateOne(CSharpApiGeneratorInput input, ILogger logger) => input switch
    {
        NuGetManifestInput m => new NuGetAssemblySource(m.RootDirectory, m.ApiCachePath, logger),
        NuGetPackagesInput p => CreateFromPackages(p, logger),
        LocalAssembliesInput l => new LocalAssemblySource(l.Tfm, l.AssemblyPaths, l.FallbackSearchPaths),
        CustomInput c => c.Source,
        _ => throw new ArgumentException($"Unknown input shape: {input.GetType().FullName}", nameof(input))
    };

    /// <summary>
    /// Synthesizes a transient <c>nuget-packages.json</c> under <paramref name="input"/>'s cache path and
    /// returns a <see cref="NuGetAssemblySource"/> pointing at the scratch directory.
    /// </summary>
    /// <param name="input">Inline-package input.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>A source ready to walk.</returns>
    internal static NuGetAssemblySource CreateFromPackages(NuGetPackagesInput input, ILogger logger)
    {
        var scratch = Path.Combine(input.ApiCachePath, SynthesizedManifestDirectory);
        Directory.CreateDirectory(scratch);
        var manifestPath = Path.Combine(scratch, ManifestFileName);
        File.WriteAllBytes(manifestPath, BuildManifestJson(input));
        return new(scratch, input.ApiCachePath, logger);
    }

    /// <summary>Writes the inline package list as a SourceDocParser <c>nuget-packages.json</c> document.</summary>
    /// <param name="input">Inline-package input.</param>
    /// <returns>UTF-8 manifest bytes.</returns>
    internal static byte[] BuildManifestJson(NuGetPackagesInput input)
    {
        ArrayBufferWriter<byte> sink = new(initialCapacity: 256);
        using (Utf8JsonWriter writer = new(sink, new() { Indented = false }))
        {
            writer.WriteStartObject();

            writer.WritePropertyName("nugetPackageOwners"u8);
            writer.WriteStartArray();
            writer.WriteEndArray();

            writer.WritePropertyName("tfmPreference"u8);
            writer.WriteStartArray();
            for (var i = 0; i < input.TfmPreference.Length; i++)
            {
                writer.WriteStringValue(input.TfmPreference[i]);
            }

            writer.WriteEndArray();

            writer.WritePropertyName("additionalPackages"u8);
            writer.WriteStartArray();
            for (var i = 0; i < input.Packages.Length; i++)
            {
                var pkg = input.Packages[i];
                writer.WriteStartObject();
                writer.WriteString("id"u8, pkg.PackageId);
                writer.WriteString("version"u8, pkg.Version);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WritePropertyName("excludePackages"u8);
            writer.WriteStartArray();
            writer.WriteEndArray();

            writer.WritePropertyName("excludePackagePrefixes"u8);
            writer.WriteStartArray();
            writer.WriteEndArray();

            writer.WritePropertyName("referencePackages"u8);
            writer.WriteStartArray();
            writer.WriteEndArray();

            writer.WritePropertyName("tfmOverrides"u8);
            writer.WriteStartObject();
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return [.. sink.WrittenSpan];
    }
}
