// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Reads a <c>nuget-packages.json</c> manifest under <paramref name="RootDirectory"/>
/// and fetches every listed package into <paramref name="ApiCachePath"/> via
/// SourceDocParser's <c>NuGetAssemblySource</c>.
/// </summary>
/// <param name="RootDirectory">Repository root containing <c>nuget-packages.json</c>.</param>
/// <param name="ApiCachePath">Destination root for fetched packages.</param>
public sealed record NuGetManifestInput(string RootDirectory, string ApiCachePath) : CSharpApiGeneratorInput;
