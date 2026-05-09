// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace NuStreamDocs.Versions;

/// <summary>One entry in <c>versions.json</c> (mike-compatible shape).</summary>
/// <param name="Version">Stable version identifier (e.g. <c>0.4.2</c>).</param>
/// <param name="Title">Human-readable selector label.</param>
/// <param name="Aliases">UTF-8 mirror identifiers like <c>latest</c> or <c>stable</c>.</param>
public readonly record struct VersionEntry(string Version, string Title, byte[][] Aliases);
