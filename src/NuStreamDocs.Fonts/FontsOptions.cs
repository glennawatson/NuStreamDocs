// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using NuStreamDocs.Common;

namespace NuStreamDocs.Fonts;

/// <summary>Configuration for <c>FontsPlugin</c>.</summary>
/// <param name="Faces">Declared font families to self-host.</param>
/// <param name="CacheDirectory">Directory for the content-addressed download cache; empty falls back to a default under the temp path.</param>
/// <param name="Offline">When true, a download-cache miss is an error instead of a network fetch (for reproducible CI once the cache is warm).</param>
/// <param name="OutputSubdirectory">Site-relative directory the font files and <c>fonts.css</c> are written under (e.g. <c>assets/fonts</c>).</param>
public readonly record struct FontsOptions(
    FontFace[] Faces,
    DirectoryPath CacheDirectory,
    bool Offline,
    PathSegment OutputSubdirectory)
{
    /// <summary>Gets the option set with all defaults populated (no faces — themes fall back to a system-font stack).</summary>
    public static FontsOptions Default { get; } = new(
        Faces: [],
        CacheDirectory: default,
        Offline: false,
        OutputSubdirectory: "assets/fonts");
}
