// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.CSharpApiGenerator;

/// <summary>
/// Inline NuGet-package reference handed to <see cref="NuGetPackagesInput"/>.
/// </summary>
/// <param name="PackageId">NuGet package identifier (e.g. <c>ReactiveUI</c>).</param>
/// <param name="Version">Exact version string (e.g. <c>20.0.0</c>); never a range.</param>
public readonly record struct NuGetPackageReference(string PackageId, string Version);
