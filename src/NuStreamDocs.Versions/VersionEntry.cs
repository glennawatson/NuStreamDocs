// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Versions;

/// <summary>
/// One entry in <c>versions.json</c>. Shape matches mike (the mkdocs
/// versioning helper) so existing themes that bundle a mike-aware
/// version selector light up without translation.
/// </summary>
/// <remarks>
/// <see cref="Aliases"/> is stored as UTF-8 bytes — the manifest is JSON
/// and aliases flow byte-to-byte through <c>Utf8JsonWriter</c> /
/// <c>Utf8JsonReader</c> without a detour via <see cref="string"/>.
/// </remarks>
/// <param name="Version">Stable identifier — typically the SemVer release, e.g. <c>0.4.2</c>.</param>
/// <param name="Title">Human-readable label rendered in the selector, e.g. <c>0.4.x (latest)</c>.</param>
/// <param name="Aliases">UTF-8 mirror identifiers like <c>latest</c> or <c>stable</c>; surface via redirects in the parent site.</param>
public readonly record struct VersionEntry(string Version, string Title, byte[][] Aliases);
