// Copyright (c) 2026 Glenn Watson and Contributors. All rights reserved.
// Glenn Watson and Contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace NuStreamDocs.Metadata;

/// <summary>
/// Read-only lookup of "extra frontmatter to inject" per relative
/// page path. Keys are forward-slash-normalised paths (e.g.
/// <c>guide/intro.md</c>); values are the merged YAML body bytes
/// (no surrounding <c>---</c> delimiters, ASCII top-level keys only).
/// </summary>
/// <remarks>
/// Built once at <see cref="MetadataPlugin"/>'s configure phase from
/// directory <c>_meta.yml</c> files and per-page sidecars; queried per
/// page during the preprocessor pass. Frozen so the read-many path is
/// branchless after construction.
/// </remarks>
/// <param name="byPath">Path → merged-body lookup; case-insensitive on the relative-path key to absorb cross-OS path-casing differences.</param>
internal sealed class MetadataRegistry(FrozenDictionary<string, byte[]> byPath)
{
    /// <summary>Gets the empty registry — used when no metadata files were found.</summary>
    public static MetadataRegistry Empty { get; } = new(FrozenDictionary<string, byte[]>.Empty);

    /// <summary>Looks up the merged-body bytes for <paramref name="relativePath"/>, or returns an empty span when nothing to inject.</summary>
    /// <param name="relativePath">Forward-slash-normalised page path.</param>
    /// <returns>Merged YAML body bytes (no <c>---</c> delimiters); empty when absent.</returns>
    public ReadOnlySpan<byte> ExtraFor(string relativePath) =>
        byPath.TryGetValue(relativePath, out var bytes) ? bytes : [];
}
